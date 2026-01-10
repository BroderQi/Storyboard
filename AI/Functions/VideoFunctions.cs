using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace 分镜大师.AI.Functions;

/// <summary>
/// 视频分析函数插件
/// </summary>
public class VideoAnalysisFunctions
{
    /// <summary>
    /// 分析视频时长并转换为可读格式
    /// </summary>
    [KernelFunction, Description("将秒数转换为可读的时间格式")]
    public string FormatDuration(
        [Description("视频时长（秒）")] double seconds)
    {
        var timeSpan = TimeSpan.FromSeconds(seconds);
        return timeSpan.ToString(@"hh\:mm\:ss");
    }

    /// <summary>
    /// 计算视频帧率
    /// </summary>
    [KernelFunction, Description("根据总帧数和时长计算帧率")]
    public double CalculateFrameRate(
        [Description("总帧数")] int totalFrames,
        [Description("视频时长（秒）")] double duration)
    {
        if (duration <= 0) return 0;
        return Math.Round(totalFrames / duration, 2);
    }

    /// <summary>
    /// 计算视频比特率
    /// </summary>
    [KernelFunction, Description("根据文件大小和时长计算比特率")]
    public string CalculateBitrate(
        [Description("文件大小（字节）")] long fileSize,
        [Description("视频时长（秒）")] double duration)
    {
        if (duration <= 0) return "0 kbps";
        var bitrate = (fileSize * 8) / duration / 1000; // kbps
        return $"{Math.Round(bitrate, 2)} kbps";
    }

    /// <summary>
    /// 验证分辨率格式
    /// </summary>
    [KernelFunction, Description("验证并格式化视频分辨率")]
    public string ValidateResolution(
        [Description("宽度")] int width,
        [Description("高度")] int height)
    {
        return $"{width}x{height}";
    }
}

/// <summary>
/// 场景描述函数插件
/// </summary>
public class SceneDescriptionFunctions
{
    /// <summary>
    /// 生成场景标签
    /// </summary>
    [KernelFunction, Description("根据场景描述生成关键标签")]
    public string GenerateTags(
        [Description("场景描述")] string description)
    {
        // 这里可以集成NLP来提取关键词
        // 简化实现：返回基本标签
        var tags = new List<string>();
        
        if (description.Contains("室内")) tags.Add("室内");
        if (description.Contains("室外")) tags.Add("室外");
        if (description.Contains("白天")) tags.Add("白天");
        if (description.Contains("夜晚")) tags.Add("夜晚");
        if (description.Contains("人物")) tags.Add("人物");
        
        return string.Join(", ", tags);
    }

    /// <summary>
    /// 估算场景复杂度
    /// </summary>
    [KernelFunction, Description("根据场景描述估算场景复杂度（1-10）")]
    public int EstimateComplexity(
        [Description("场景描述")] string description)
    {
        var complexity = 5; // 基础复杂度
        
        if (description.Contains("多个")) complexity += 2;
        if (description.Contains("特效")) complexity += 2;
        if (description.Contains("动作")) complexity += 1;
        if (description.Length > 100) complexity += 1;
        
        return Math.Min(complexity, 10);
    }
}

/// <summary>
/// 镜头类型函数插件
/// </summary>
public class ShotTypeFunctions
{
    private static readonly Dictionary<string, string> ShotTypeDescriptions = new()
    {
        ["特写"] = "Close-up Shot - 聚焦于主体细节",
        ["近景"] = "Medium Close-up - 展示主体上半身",
        ["中景"] = "Medium Shot - 展示主体全身或大部分",
        ["全景"] = "Full Shot - 展示完整场景",
        ["远景"] = "Long Shot - 展示广阔的环境"
    };

    /// <summary>
    /// 获取镜头类型描述
    /// </summary>
    [KernelFunction, Description("获取镜头类型的详细描述")]
    public string GetShotTypeDescription(
        [Description("镜头类型")] string shotType)
    {
        return ShotTypeDescriptions.TryGetValue(shotType, out var desc) 
            ? desc 
            : "未知镜头类型";
    }

    /// <summary>
    /// 推荐镜头类型
    /// </summary>
    [KernelFunction, Description("根据场景内容推荐合适的镜头类型")]
    public string RecommendShotType(
        [Description("场景描述")] string sceneDescription)
    {
        if (sceneDescription.Contains("表情") || sceneDescription.Contains("细节"))
            return "特写";
        if (sceneDescription.Contains("对话") || sceneDescription.Contains("交流"))
            return "近景";
        if (sceneDescription.Contains("动作") || sceneDescription.Contains("走动"))
            return "中景";
        if (sceneDescription.Contains("环境") || sceneDescription.Contains("背景"))
            return "全景";
        
        return "中景"; // 默认
    }
}

/// <summary>
/// 时间码函数插件
/// </summary>
public class TimecodeFunctions
{
    /// <summary>
    /// 解析时间码
    /// </summary>
    [KernelFunction, Description("将时间码字符串转换为秒数")]
    public double ParseTimecode(
        [Description("时间码（格式：HH:MM:SS 或 MM:SS）")] string timecode)
    {
        var parts = timecode.Split(':');
        double seconds = 0;
        
        if (parts.Length == 3)
        {
            seconds = int.Parse(parts[0]) * 3600 + int.Parse(parts[1]) * 60 + double.Parse(parts[2]);
        }
        else if (parts.Length == 2)
        {
            seconds = int.Parse(parts[0]) * 60 + double.Parse(parts[1]);
        }
        
        return seconds;
    }

    /// <summary>
    /// 格式化时间码
    /// </summary>
    [KernelFunction, Description("将秒数转换为时间码字符串")]
    public string FormatTimecode(
        [Description("秒数")] double seconds,
        [Description("是否包含小时（默认true）")] bool includeHours = true)
    {
        var timeSpan = TimeSpan.FromSeconds(seconds);
        return includeHours 
            ? timeSpan.ToString(@"hh\:mm\:ss\.ff") 
            : timeSpan.ToString(@"mm\:ss\.ff");
    }

    /// <summary>
    /// 计算时长
    /// </summary>
    [KernelFunction, Description("计算两个时间码之间的时长")]
    public double CalculateDuration(
        [Description("起始时间码")] string startTimecode,
        [Description("结束时间码")] string endTimecode)
    {
        var start = ParseTimecode(startTimecode);
        var end = ParseTimecode(endTimecode);
        return end - start;
    }
}
