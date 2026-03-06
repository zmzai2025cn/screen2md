using System.Runtime.InteropServices;

namespace Screen2MD.Testing;

/// <summary>
/// 标记测试仅在特定平台运行
/// 这是方案5的实现：明确的平台测试标记
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class PlatformSpecificAttribute : Attribute
{
    public OSPlatform RequiredPlatform { get; }
    public string Reason { get; }
    public bool SkipOnPlatform { get; set; } = true;

    public PlatformSpecificAttribute(OSPlatform platform, string reason)
    {
        RequiredPlatform = platform;
        Reason = reason;
    }

    public bool ShouldSkip()
    {
        if (!SkipOnPlatform) return false;
        
        return RequiredPlatform switch
        {
            OSPlatform.Windows => !RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows),
            OSPlatform.Linux => !RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux),
            OSPlatform.OSX => !RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX),
            _ => true
        };
    }
}

public enum OSPlatform
{
    Windows,
    Linux,
    OSX
}

/// <summary>
/// 标记测试需要真实截图功能
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequiresScreenCaptureAttribute : PlatformSpecificAttribute
{
    public RequiresScreenCaptureAttribute() 
        : base(OSPlatform.Windows, "Requires real screen capture capability")
    {
    }
}

/// <summary>
/// 标记测试需要 OCR 引擎
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequiresOcrEngineAttribute : Attribute
{
    public string[] RequiredLanguages { get; }

    public RequiresOcrEngineAttribute(params string[] requiredLanguages)
    {
        RequiredLanguages = requiredLanguages;
    }
}

/// <summary>
/// 标记测试使用模拟数据
/// Linux 环境下可运行
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class UsesMockDataAttribute : Attribute
{
    public string Description { get; }

    public UsesMockDataAttribute(string description)
    {
        Description = description;
    }
}

/// <summary>
/// 契约测试标记
/// 验证实现是否符合抽象契约
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ContractTestAttribute : Attribute
{
    public Type ContractType { get; }

    public ContractTestAttribute(Type contractType)
    {
        ContractType = contractType;
    }
}