using System.Windows;
using Storyboard.AI;
using Storyboard.AI.Core;

namespace Storyboard.Views.Windows;

/// <summary>
/// AI服务配置窗口
/// </summary>
public partial class AIConfigWindow : Window
{
    private readonly AIServiceManager _aiManager;

    public AIConfigWindow(AIServiceManager aiManager)
    {
        InitializeComponent();
        _aiManager = aiManager;
        LoadProviders();
    }

    private void LoadProviders()
    {
        var providers = _aiManager.GetAvailableProviders();
        // 这里可以绑定到UI控件
        // 例如: ProvidersListBox.ItemsSource = providers;
    }

    private async void ValidateButton_Click(object sender, RoutedEventArgs e)
    {
        var results = await _aiManager.ValidateAllProvidersAsync();
        
        foreach (var (provider, isValid) in results)
        {
            MessageBox.Show(
                $"{provider}: {(isValid ? "配置有效 ✅" : "配置无效 ❌")}",
                "验证结果",
                MessageBoxButton.OK,
                isValid ? MessageBoxImage.Information : MessageBoxImage.Warning
            );
        }
    }

    private void TestChatButton_Click(object sender, RoutedEventArgs e)
    {
        // 测试聊天功能
        TestChat();
    }

    private async void TestChat()
    {
        try
        {
            var response = await _aiManager.ChatDirectAsync(
                "你好，请介绍一下你自己",
                temperature: 0.7
            );

            MessageBox.Show(
                response,
                "AI响应",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"测试失败: {ex.Message}",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
}
