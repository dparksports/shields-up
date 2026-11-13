using ShieldsUp.Core.Models;
using ShieldsUp.Core.Services;

namespace ShieldsUp.Pages;

public sealed partial class TasksPage : Page
{
    private List<ScheduledTaskItem> _allTasks = new();

    public TasksPage()
    {
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadTasks();
        ApplyResponsiveColumns(ActualWidth);
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyResponsiveColumns(e.NewSize.Width);

    /// <summary>Task name and state survive the narrow layout; path and user do not.</summary>
    private void ApplyResponsiveColumns(double width) =>
        Helpers.GridColumns.Apply(TasksGrid, width,
            ("Path", 920),
            ("User", 920),
            ("Scope", 820),
            ("Action", 760));

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e) => await LoadTasks();

    private async Task LoadTasks()
    {
        _allTasks = await TaskSchedulerService.Instance.LoadTasksAsync();
        DispatcherQueue.TryEnqueue(ApplyFilter);
    }

    private void FilterChanged(object sender, RoutedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (RunningRadio == null || TasksGrid == null || TaskCountText == null) return;
        IEnumerable<ScheduledTaskItem> filtered = _allTasks;

        if (ThirdPartyRadio?.IsChecked == true)
            filtered = filtered.Where(t => t.Scope == "Third-party");
        else if (RunningRadio.IsChecked == true)
            filtered = filtered.Where(t => t.State == "Running");
        else if (ReadyRadio.IsChecked == true)
            filtered = filtered.Where(t => t.State == "Ready");
        else if (DisabledRadio.IsChecked == true)
            filtered = filtered.Where(t => t.State == "Disabled");

        var list = filtered.OrderBy(t => t.TaskPath).ThenBy(t => t.TaskName).ToList();
        TasksGrid.ItemsSource = list;
        int tpCount = _allTasks.Count(t => t.Scope == "Third-party");
        TaskCountText.Text = $"{list.Count} tasks" +
            (AllRadio.IsChecked == true
                ? $" ({tpCount} third-party, {_allTasks.Count(t => t.State == "Running")} running, {_allTasks.Count(t => t.State == "Ready")} ready, {_allTasks.Count(t => t.State == "Disabled")} disabled)"
                : "");
    }

    private string GetSelectedTaskFullPath()
    {
        if (TasksGrid.SelectedItem is not ScheduledTaskItem task) return null;
        if (!string.IsNullOrEmpty(task.FullPath)) return task.FullPath;
        string path = task.TaskPath;
        if (path == "\\") return "\\" + task.TaskName;
        if (path == "Windows System") return $@"\Microsoft\Windows\{task.TaskName}";
        if (!path.StartsWith("\\")) path = $@"\Microsoft\Windows\{path}";
        return path + "\\" + task.TaskName;
    }

    private async void DisableTask_Click(object sender, RoutedEventArgs e)
    {
        string path = GetSelectedTaskFullPath();
        if (path == null) return;
        if (!await Helpers.Dialogs.Confirm($"Disable scheduled task '{path.Split('\\').Last()}'?", "Disable task",
                yes: "Disable", no: "Cancel")) return;
        try
        {
            await TaskSchedulerService.Instance.SetTaskEnabledAsync(path, false);
            await LoadTasks();
        }
        catch (Exception ex)
        {
            await Helpers.Dialogs.Error($"Failed to disable: {ex.Message}", "Error");
        }
    }

    private async void EnableTask_Click(object sender, RoutedEventArgs e)
    {
        string path = GetSelectedTaskFullPath();
        if (path == null) return;
        if (!await Helpers.Dialogs.Confirm($"Enable scheduled task '{path.Split('\\').Last()}'?", "Enable task",
                yes: "Enable", no: "Cancel")) return;
        try
        {
            await TaskSchedulerService.Instance.SetTaskEnabledAsync(path, true);
            await LoadTasks();
        }
        catch (Exception ex)
        {
            await Helpers.Dialogs.Error($"Failed to enable: {ex.Message}", "Error");
        }
    }

    private async void RunTask_Click(object sender, RoutedEventArgs e)
    {
        string path = GetSelectedTaskFullPath();
        if (path == null) return;
        if (!await Helpers.Dialogs.Confirm($"Run scheduled task '{path.Split('\\').Last()}' now?", "Run task",
                yes: "Run", no: "Cancel")) return;
        try
        {
            await TaskSchedulerService.Instance.RunTaskAsync(path);
            await Task.Delay(500);
            await LoadTasks();
        }
        catch (Exception ex)
        {
            await Helpers.Dialogs.Error($"Failed to run: {ex.Message}", "Error");
        }
    }

    private async void StopTask_Click(object sender, RoutedEventArgs e)
    {
        string path = GetSelectedTaskFullPath();
        if (path == null) return;
        if (!await Helpers.Dialogs.Confirm($"Stop the running task '{path.Split('\\').Last()}'?", "Stop task",
                yes: "Stop", no: "Cancel")) return;
        try
        {
            await TaskSchedulerService.Instance.StopTaskAsync(path);
            await LoadTasks();
        }
        catch (Exception ex)
        {
            await Helpers.Dialogs.Error($"Failed to stop: {ex.Message}", "Error");
        }
    }

    private async void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (TasksGrid.SelectedItem is not ScheduledTaskItem task) return;
        if (!await Helpers.Dialogs.Confirm($"Delete task '{task.TaskName}'? This cannot be undone.",
                "Confirm Delete"))
            return;

        string path = GetSelectedTaskFullPath();
        if (path == null) return;
        try
        {
            await TaskSchedulerService.Instance.DeleteTaskAsync(path);
            await LoadTasks();
        }
        catch (Exception ex)
        {
            await Helpers.Dialogs.Error($"Failed to delete: {ex.Message}", "Error");
        }
    }
}
