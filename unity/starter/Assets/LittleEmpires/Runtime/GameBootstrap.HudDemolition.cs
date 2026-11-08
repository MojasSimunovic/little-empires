using LittleEmpires.Core;
using UnityEngine.UIElements;

namespace LittleEmpires.Unity
{
    public sealed partial class GameBootstrap
    {
        private static string DemolitionReason(DemolitionResult result) =>
            result.Failure == DemolitionFailure.InsufficientHousing
                ? $"Build housing for {result.HousingShortfall} more resident(s) first."
                : "This building is no longer available.";

        private void Demolish(int instanceId, string name)
        {
            var result = _session.DemolishBuilding(instanceId);
            if (!result.Succeeded)
            {
                _message = DemolitionReason(result);
                return;
            }

            _selectedBuilding = -1;
            _selected = null;
            _showInspector = false;
            SelectTool(Tool.Inspect);
            _message = $"{name} demolished.";
            Refresh();
        }

        private sealed partial class IslandHud
        {
            private VisualElement _demolitionOverlay;
            private Label _demolitionTitle, _demolitionWarning;
            private Button _cancelDemolition;
            private BuildingView _pendingDemolition;

            public bool HasModal => _pendingDemolition != null;

            private void BindDemolition()
            {
                _demolitionOverlay = _root.Q("demolition-overlay");
                _demolitionTitle = _root.Q<Label>("demolition-title");
                _demolitionWarning = _root.Q<Label>("demolition-warning");
                _cancelDemolition = _root.Q<Button>("cancel-demolition");
                _cancelDemolition.clicked += CloseDemolition;
                _root.Q<Button>("confirm-demolition").clicked += () =>
                {
                    var building = _pendingDemolition;
                    if (building == null) return;
                    CloseDemolition();
                    _game.Demolish(building.InstanceId, building.Name);
                    Sync();
                };
            }

            public void CloseDemolition()
            {
                _cancelDemolition.Blur();
                _pendingDemolition = null;
                _demolitionOverlay.style.display = DisplayStyle.None;
                SetBackgroundEnabled(true);
            }

            private void DemolitionControls(BuildingView building)
            {
                Detail(_body, "Demolition", "section");
                var preview = _game._session.PreviewDemolition(building.InstanceId);
                if (!preview.Succeeded) Detail(_body, DemolitionReason(preview), "warning");
                var button = Button("Demolish", () => OpenDemolition(building));
                button.AddToClassList("destructive");
                button.SetEnabled(preview.Succeeded);
                _body.Add(button);
            }

            private void OpenDemolition(BuildingView building)
            {
                var preview = _game._session.PreviewDemolition(building.InstanceId);
                if (!preview.Succeeded)
                {
                    _game._message = DemolitionReason(preview);
                    return;
                }

                _game.CancelRoad();
                _game._view.ShowFootprintPreview(null);
                HideTooltip();
                _pendingDemolition = building;
                _demolitionTitle.text = $"Demolish {building.Name}?";
                var warning = "The plot will be freed immediately. Materials will not be refunded.";
                if (!building.IsComplete) warning += "\nConstruction will be canceled.";
                if (building.IsUpgrading) warning += "\nThe upgrade will be canceled.";
                if (IsCivicHall(building.Id))
                {
                    warning += "\nHall administration will be unavailable until a new Hall is completed. The current tax policy stays in effect.";
                    if (building.Id == BuildingCatalog.TownHallId)
                        warning += " The settlement returns to Village tier. Existing Town buildings remain.";
                }
                _demolitionWarning.text = warning;
                _demolitionOverlay.style.display = DisplayStyle.Flex;
                SetBackgroundEnabled(false);
                _cancelDemolition.Focus();
            }

            private void SetBackgroundEnabled(bool enabled)
            {
                _root.Q("top-bar").SetEnabled(enabled);
                _root.Q("build-dock").SetEnabled(enabled);
                _inspector.SetEnabled(enabled);
            }
        }
    }
}
