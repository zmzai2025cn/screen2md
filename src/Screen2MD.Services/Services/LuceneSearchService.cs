using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Screen2MD.Kernel.Interfaces;
using Screen2MD.Services.Interfaces;

namespace Screen2MD.Services.Services;

/// <summary>
/// Lucene.NET 全文搜索服务 - 替代 SQLite FTS5
/// 支持中文、英文全文检索，跨平台，零外部依赖
/// </summary>
public sealed class LuceneSearchService : IFullTextSearchService, IDisposable
{
    private const string LUCENE_VERSION = "LUCENE_48";
    private readonly string _indexPath;
    private readonly IKernelLogger? _logger;
    private readonly LuceneVersion _luceneVersion;
    
    private FSDirectory? _luceneDirectory;
    private StandardAnalyzer? _analyzer;
    private IndexWriter? _writer;
    private bool _disposed;
    private readonly object _writerLock = new();

    public string Name => nameof(LuceneSearchService);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    public LuceneSearchService(string? indexPath = null, IKernelLogger? logger = null)
    {
        _indexPath = indexPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Screen2MD", "lucene_index");
        _logger = logger;
        _luceneVersion = LuceneVersion.LUCENE_48;
        
        // 确保索引目录存在
        System.IO.Directory.CreateDirectory(_indexPath);
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LuceneSearchService));

            _luceneDirectory = FSDirectory.Open(_indexPath);
            _analyzer = new StandardAnalyzer(_luceneVersion);
            
            // 创建或打开索引
            var config = new IndexWriterConfig(_luceneVersion, _analyzer)
            {
                OpenMode = OpenMode.CREATE_OR_APPEND
            };
            
            _writer = new IndexWriter(_luceneDirectory, config);
            
            HealthStatus = HealthStatus.Healthy;
            _logger?.LogInformation($"LuceneSearchService initialized. Index path: {_indexPath}");
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            HealthStatus = HealthStatus.Unhealthy;
            _logger?.LogError($"Failed to initialize LuceneSearchService: {ex.Message}");
            throw;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// 索引捕获内容
    /// </summary>
    public async Task IndexCaptureAsync(CaptureDocument document, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LuceneSearchService));
        if (_writer == null) throw new InvalidOperationException("Service not initialized");

        try
        {
            await Task.Run(() =>
            {
                lock (_writerLock)
                {
                    // 先删除旧文档（如果存在）
                    var term = new Term("capture_id", document.Id);
                    _writer.DeleteDocuments(term);

                    // 创建新文档
                    var doc = new Document();
                    
                    // 存储字段（可检索且可返回）
                    doc.Add(new StringField("capture_id", document.Id, Field.Store.YES));
                    doc.Add(new TextField("title", document.Title ?? "", Field.Store.YES));
                    doc.Add(new TextField("content", document.Content ?? "", Field.Store.YES));
                    doc.Add(new TextField("process_name", document.ProcessName ?? "", Field.Store.YES));
                    doc.Add(new TextField("window_title", document.WindowTitle ?? "", Field.Store.YES));
                    doc.Add(new TextField("tags", string.Join(" ", document.Tags ?? Array.Empty<string>()), Field.Store.YES));
                    
                    // 存储但不索引的字段
                    doc.Add(new StoredField("file_path", document.FilePath ?? ""));
                    doc.Add(new StoredField("timestamp", document.Timestamp.ToUnixTimeMilliseconds()));
                    doc.Add(new StoredField("display_index", document.DisplayIndex));
                    
                    // 时间戳用于范围查询（ NumericDocValuesField 用于排序/范围， StoredField 用于存储）
                    doc.Add(new Int64Field("timestamp_sort", document.Timestamp.ToUnixTimeMilliseconds(), Field.Store.NO));

                    _writer.AddDocument(doc);
                    _writer.Commit();
                    
                    _logger?.LogDebug($"Indexed document: {document.Id}");
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to index document: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 搜索
    /// </summary>
    public async Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LuceneSearchService));
        if (_luceneDirectory == null || _analyzer == null) throw new InvalidOperationException("Service not initialized");

        try
        {
            return await Task.Run(() =>
            {
                using var reader = DirectoryReader.Open(_luceneDirectory);
                var searcher = new IndexSearcher(reader);

                // 构建查询
                Query luceneQuery = BuildLuceneQuery(query);

                // 执行搜索
                int maxResults = query.PageSize * query.PageNumber;
                var topDocs = searcher.Search(luceneQuery, maxResults);

                // 计算总数
                int totalCount = topDocs.TotalHits;

                // 分页
                var results = new List<SearchResultItem>();
                int start = (query.PageNumber - 1) * query.PageSize;
                int end = Math.Min(start + query.PageSize, topDocs.ScoreDocs.Length);

                for (int i = start; i < end; i++)
                {
                    var doc = searcher.Doc(topDocs.ScoreDocs[i].Doc);
                    results.Add(ConvertToResultItem(doc, topDocs.ScoreDocs[i].Score));
                }

                return new SearchResult
                {
                    Items = results,
                    TotalCount = totalCount,
                    PageNumber = query.PageNumber,
                    PageSize = query.PageSize,
                    Query = query.Keywords ?? ""
                };
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Search failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 构建 Lucene 查询
    /// </summary>
    private Query BuildLuceneQuery(SearchQuery query)
    {
        var queries = new List<Query>();

        // 关键词搜索
        if (!string.IsNullOrWhiteSpace(query.Keywords))
        {
            var keywordQuery = BuildKeywordQuery(query.Keywords, query.ExactMatch, query.PrefixMatch);
            queries.Add(keywordQuery);
        }

        // 进程名过滤
        if (!string.IsNullOrWhiteSpace(query.ProcessName))
        {
            queries.Add(new TermQuery(new Term("process_name", query.ProcessName.ToLowerInvariant())));
        }

        // 时间范围过滤
        if (query.StartTime.HasValue || query.EndTime.HasValue)
        {
            long start = query.StartTime?.ToUnixTimeMilliseconds() ?? long.MinValue;
            long end = query.EndTime?.ToUnixTimeMilliseconds() ?? long.MaxValue;
            queries.Add(NumericRangeQuery.NewInt64Range("timestamp_sort", start, end, true, true));
        }

        // 组合查询
        if (queries.Count == 0)
        {
            return new MatchAllDocsQuery();
        }
        else if (queries.Count == 1)
        {
            return queries[0];
        }
        else
        {
            var booleanQuery = new BooleanQuery();
            foreach (var q in queries)
            {
                booleanQuery.Add(q, Occur.MUST);
            }
            return booleanQuery;
        }
    }

    /// <summary>
    /// 构建关键词查询
    /// </summary>
    private Query BuildKeywordQuery(string keywords, bool exactMatch, bool prefixMatch)
    {
        var searchTerm = keywords.Trim();

        if (exactMatch)
        {
            // 精确匹配 - 在 title 或 content 中精确匹配
            var phraseQuery = new PhraseQuery();
            foreach (var word in searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                phraseQuery.Add(new Term("title", word.ToLowerInvariant()));
            }
            return phraseQuery;
        }
        else if (prefixMatch)
        {
            // 前缀匹配
            return new PrefixQuery(new Term("title", searchTerm.ToLowerInvariant()));
        }
        else
        {
            // 全文搜索 - 跨多个字段
            var parser = new MultiFieldQueryParser(
                _luceneVersion,
                new[] { "title", "content", "process_name", "window_title", "tags" },
                _analyzer);
            
            parser.DefaultOperator = Operator.OR;
            return parser.Parse(searchTerm);
        }
    }

    /// <summary>
    /// 转换为结果项
    /// </summary>
    private SearchResultItem ConvertToResultItem(Document doc, float score)
    {
        return new SearchResultItem
        {
            CaptureId = doc.Get("capture_id"),
            Title = doc.Get("title"),
            ProcessName = doc.Get("process_name"),
            WindowTitle = doc.Get("window_title"),
            FilePath = doc.Get("file_path"),
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(doc.Get("timestamp"))),
            Rank = score
        };
    }

    /// <summary>
    /// 删除索引
    /// </summary>
    public async Task DeleteIndexAsync(string captureId, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LuceneSearchService));
        if (_writer == null) throw new InvalidOperationException("Service not initialized");

        try
        {
            await Task.Run(() =>
            {
                lock (_writerLock)
                {
                    var term = new Term("capture_id", captureId);
                    _writer.DeleteDocuments(term);
                    _writer.Commit();
                    _logger?.LogDebug($"Deleted document: {captureId}");
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to delete document: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 优化索引（Lucene 自动优化，此方法仅做日志）
    /// </summary>
    public async Task OptimizeAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LuceneSearchService));
        if (_writer == null) throw new InvalidOperationException("Service not initialized");

        try
        {
            await Task.Run(() =>
            {
                lock (_writerLock)
                {
                    // Lucene 4+ 使用合并策略自动优化
                    _writer.ForceMerge(1, true);
                    _logger?.LogInformation("Search index optimized");
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to optimize index: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    public async Task<SearchStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LuceneSearchService));
        if (_luceneDirectory == null) throw new InvalidOperationException("Service not initialized");

        try
        {
            return await Task.Run(() =>
            {
                using var reader = DirectoryReader.Open(_luceneDirectory);
                
                var stats = new SearchStatistics
                {
                    TotalDocuments = reader.NumDocs
                };

                // 获取时间范围
                var searcher = new IndexSearcher(reader);
                var allDocs = searcher.Search(new MatchAllDocsQuery(), int.MaxValue);
                
                if (allDocs.ScoreDocs.Length > 0)
                {
                    var timestamps = allDocs.ScoreDocs
                        .Select(sd => searcher.Doc(sd.Doc))
                        .Select(d => DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(d.Get("timestamp"))))
                        .OrderBy(t => t)
                        .ToList();

                    stats.OldestDocument = timestamps.First();
                    stats.NewestDocument = timestamps.Last();
                }

                return stats;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to get statistics: {ex.Message}");
            return new SearchStatistics();
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            
            try
            {
                _writer?.Commit();
                _writer?.Dispose();
                _analyzer?.Dispose();
                _luceneDirectory?.Dispose();
                
                _logger?.LogInformation("LuceneSearchService disposed");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error during disposal: {ex.Message}");
            }
        }
    }
}
