using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NoCodeVision.Views.Controls;

public partial class AppleMasterList : UserControl
{
    public AppleMasterList()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(AppleMasterList),
            new PropertyMetadata("列表"));

    public static readonly DependencyProperty HeaderIconProperty =
        DependencyProperty.Register(nameof(HeaderIcon), typeof(string), typeof(AppleMasterList),
            new PropertyMetadata(""));

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(AppleMasterList),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(AppleMasterList),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(AppleMasterList),
            new PropertyMetadata(null));

    public static readonly DependencyProperty AddCommandProperty =
        DependencyProperty.Register(nameof(AddCommand), typeof(ICommand), typeof(AppleMasterList),
            new PropertyMetadata(null));

    public static readonly DependencyProperty AddTextProperty =
        DependencyProperty.Register(nameof(AddText), typeof(string), typeof(AppleMasterList),
            new PropertyMetadata("添加"));

    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.Register(nameof(DeleteCommand), typeof(ICommand), typeof(AppleMasterList),
            new PropertyMetadata(null));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string HeaderIcon
    {
        get => (string)GetValue(HeaderIconProperty);
        set => SetValue(HeaderIconProperty, value);
    }

    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public DataTemplate ItemTemplate
    {
        get => (DataTemplate)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public ICommand AddCommand
    {
        get => (ICommand)GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    /// <summary>「添加」按钮显示文本（不同页面可定制，如「添加逻辑视觉」）。</summary>
    public string AddText
    {
        get => (string)GetValue(AddTextProperty);
        set => SetValue(AddTextProperty, value);
    }

    public ICommand DeleteCommand
    {
        get => (ICommand)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }
}
// 温启志：18719361399  混淆: 温m启g志x：b1e8d7r1l9q3q6i1w3o9h9
