# Screen2MD Enterprise - 安全加固指南

## 安全概述

本文档描述Screen2MD Enterprise的安全特性和加固措施。

## 已实施的安全措施

### 1. 输入验证 ✅

#### 路径遍历防护
```csharp
// 所有文件路径都经过验证
if (!SecurityUtils.IsPathSafe(userPath, baseDirectory))
    throw new SecurityException("Invalid path");
```

#### 文件名净化
```csharp
// 自动移除危险字符
var safeName = SecurityUtils.SanitizeFileName(userInput);
```

### 2. 敏感数据保护 ✅

#### 自动脱敏
系统自动检测并脱敏以下内容：
- 银行卡号（16-19位数字）
- 身份证号（18位）
- 邮箱地址
- 密码字段（Password/Pwd/Secret）

#### 日志脱敏
```csharp
// 配置文件中存储敏感信息时自动加密
// 日志中自动脱敏敏感字段
_logger.LogInformation("Processing user {UserId}", userId); // OK
// 不会记录：密码、Token、密钥
```

### 3. 注入防护 ✅

#### SQL/Lucene注入防护
- 使用参数化查询
- 输入验证和净化
- 查询构建器自动转义

#### 命令注入防护
- 禁止直接执行用户输入
- 所有命令使用白名单验证

### 4. 访问控制 ✅

#### 文件权限
```bash
# Linux推荐权限
chmod 750 /var/lib/screen2md          # 程序目录
chmod 640 /var/lib/screen2md/config/* # 配置文件
chmod 600 /var/lib/screen2md/secrets/* # 密钥文件
```

#### 进程隔离
- 使用非root用户运行
- 容器运行时启用安全选项

### 5. 加密存储 ✅

#### 配置文件加密
```json
{
  "sensitive": {
    "apiKey": "[ENCRYPTED]AQIDBAUGBwgJCgsM..."
  }
}
```

#### 数据传输
- 所有网络传输使用TLS 1.3
- 证书验证严格模式

## 安全审计检查清单

### 部署前检查

- [ ] 配置文件权限设置为 640
- [ ] 运行用户为非root
- [ ] 日志目录权限正确
- [ ] 防火墙规则已配置
- [ ] 自动更新已启用
- [ ] 备份策略已配置

### 运行时监控

- [ ] 异常登录监控
- [ ] 文件访问审计日志
- [ ] 敏感操作告警
- [ ] 资源使用异常检测

## 漏洞响应

### 发现漏洞

1. **不要公开披露**
2. **发送邮件至**: security@screen2md.ai
3. **包含信息**:
   - 漏洞描述
   - 复现步骤
   - 影响范围
   - 建议修复方案

### 响应时间

| 严重程度 | 响应时间 | 修复时间 |
|---------|---------|---------|
| Critical | 24小时 | 7天 |
| High | 48小时 | 14天 |
| Medium | 7天 | 30天 |
| Low | 30天 | 90天 |

## 合规性

### 数据保护
- GDPR兼容（数据导出/删除）
- CCPA兼容（加州隐私法）

### 认证标准
- SOC 2 Type II（进行中）
- ISO 27001（规划中）

## 安全更新

### 自动更新
```bash
# 启用自动安全更新
screen2md config set security.autoUpdate true
```

### 手动更新
```bash
# 检查安全公告
screen2md security check

# 应用安全补丁
screen2md update --security-only
```

## 参考

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [CWE/SANS Top 25](https://cwe.mitre.org/top25/)
- [Microsoft Security Best Practices](https://docs.microsoft.com/en-us/security/)
