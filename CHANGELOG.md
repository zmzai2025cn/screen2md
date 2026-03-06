# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial release of Screen2MD Enterprise v3.0
- Multi-display support with change detection
- Lucene.NET-based full-text search (replaced SQLite FTS5)
- Comprehensive test suite with 359 tests
- CI/CD pipeline with GitHub Actions
- Docker containerization support
- Stress testing and chaos engineering tests
- Security vulnerability tests

## [3.0.0] - 2026-03-07

### Added
- **Core Features**
  - Automatic screen capture with configurable intervals
  - Multi-monitor support (up to 8 displays)
  - Intelligent change detection (configurable threshold)
  - OCR integration with Tesseract (multi-language support)
  - Full-text search with Lucene.NET
  - Auto-cleanup with configurable strategies
  - Privacy filter for sensitive information

- **Architecture**
  - Modular plugin-based architecture
  - Cross-platform support (Windows/Linux/macOS)
  - Dependency injection container
  - Event-driven communication
  - Async/await throughout

- **Testing**
  - 359 automated tests
  - Unit, integration, stress, and security tests
  - Chaos engineering for fault tolerance
  - Performance benchmarks
  - 90%+ code coverage for core modules

- **DevOps**
  - GitHub Actions CI/CD pipeline
  - Docker and Docker Compose support
  - Multi-platform builds (x64)
  - Automated releases
  - Code quality gates

### Changed
- Migrated from SQLite FTS5 to Lucene.NET for search
- Improved memory management with object pooling
- Enhanced error handling and recovery

### Deprecated
- FullTextSearchService (SQLite FTS5) - use LuceneSearchService instead

### Security
- Added path traversal protection
- SQL/Lucene injection prevention
- Sensitive data filtering (credit cards, IDs, passwords)
- Secure configuration storage

## Migration Guide

### From v2.x to v3.0

#### Breaking Changes
1. Configuration format updated to JSON
2. Search index format changed (reindex required)
3. API endpoints updated

#### Migration Steps
```bash
# 1. Backup existing data
cp -r ~/Screen2MD ~/Screen2MD.backup

# 2. Install v3.0
# Follow installation guide for your platform

# 3. Run migration tool
screen2md migrate --from 2.x --to 3.0

# 4. Verify migration
screen2md verify

# 5. Rebuild search index
screen2md index rebuild
```

## Release Schedule

| Version | Planned Date | Key Features |
|---------|-------------|--------------|
| 3.1.0 | Q2 2026 | Plugin system, Webhooks |
| 3.2.0 | Q3 2026 | AI-powered categorization |
| 4.0.0 | Q4 2026 | Cloud sync, mobile apps |
