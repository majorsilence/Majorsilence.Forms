using Majorsilence.Forms;
using PointOfSale.Client.Controls;
using PointOfSale.Client.Forms;
using PointOfSale.Client.Services;
using PointOfSale.Contracts;

namespace PointOfSale.Client.Panels;

public class UsersPanel : BasePanel
{
    private readonly DataGridView _grid;
    private readonly Label _statusLabel;
    private List<UserDto> _users = [];

    public UsersPanel(ApiClient api, SessionState session) : base(api, session)
    {
        AutoScroll = true;

        var heading = Controls.Add(new Label { Text = "Users", Left = 10, Top = 10, Width = 300 });
        PosStyle.Heading(heading);

        var addButton = Controls.Add(new Button { Text = "Add", Left = 10, Top = 60, Width = 120, Height = 48 });
        PosStyle.PrimaryButton(addButton);
        addButton.Click += async (_, _) => await AddAsync();

        var editButton = Controls.Add(new Button { Text = "Edit", Left = 140, Top = 60, Width = 120, Height = 48 });
        PosStyle.SecondaryButton(editButton);
        editButton.Click += async (_, _) => await EditAsync();

        _grid = Controls.Add(new DataGridView
        {
            Left = 10,
            Top = 120,
            Width = 650,
            Height = 450,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        });
        GridColumns.AddBound(_grid, nameof(UserDto.DisplayName), "Name", 280);
        GridColumns.AddBound(_grid, nameof(UserDto.Role), "Role", 150);
        GridColumns.AddBound(_grid, nameof(UserDto.IsActive), "Active", 120);

        _statusLabel = Controls.Add(new Label { Text = string.Empty, Left = 10, Top = 590, Width = 650 });
    }

    public override void LoadPanel() => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        try
        {
            _users = await Api.GetUsersAsync();
            GridColumns.Rebind(_grid, _users);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not load users: {ex.Message}";
        }
    }

    private async Task AddAsync()
    {
        using var form = new UserEditForm(null);
        if (await form.ShowDialogAsync(FindForm()!) != DialogResult.OK)
            return;

        try
        {
            await Api.CreateUserAsync(new UserCreateDto(form.DisplayName, form.Pin, form.Role));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not create user: {ex.Message}";
        }
    }

    private async Task EditAsync()
    {
        var index = _grid.SelectedRowIndex;
        if (index < 0 || index >= _users.Count)
            return;

        var user = _users[index];
        using var form = new UserEditForm(user);
        if (await form.ShowDialogAsync(FindForm()!) != DialogResult.OK)
            return;

        try
        {
            await Api.UpdateUserAsync(user.Id, new UserUpdateDto(form.DisplayName, form.Role, form.IsActive, form.NewPin));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not update user: {ex.Message}";
        }
    }
}
