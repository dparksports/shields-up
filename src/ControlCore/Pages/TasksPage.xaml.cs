using ControlCore.Core.Models;
using ControlCore.Core.Services;

namespace ControlCore.Pages;

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
    }

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

        if (RunningRadio.IsChecked == true)
            filtered = filtered.Where(t => t.State == "Running");
        else if (ReadyRadio.IsChecked == true)
            filtered = filtered.Where(t => t.State == "Ready");
        else if (DisabledRadio.IsChecked == true)
            filtered = filtered.Where(t => t.State == "Disabled");

        var list = filtered.OrderBy(t => t.TaskPath).ThenBy(t => t.TaskName).ToList();
        TasksGrid.ItemsSource = list;
        TaskCountText.Text = $"{list.Count} tasks" +
            (AllRadio.IsChecked == true
                ? $" ({_allTasks.Count(t => t.State == "Running")} running, {_allTasks.Count(t => t.State == "Ready")} ready, {_allTasks.Count(t => t.State == "Disabled")} disabled)"
                : "");
    }

    private string GetSelectedTaskFullPath()
    {
        if (TasksGrid.SelectedItem is not ScheduledTaskItem task) return null;
        // Reconstruct the full path for COM API
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
