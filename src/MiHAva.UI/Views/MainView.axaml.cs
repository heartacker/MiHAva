using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MiHAva.UI.Views;

public partial class MainView : UserControl
{
    private bool _isDraggingRooms;
    private Point _roomsDragStartPoint;
    private Vector _roomsDragStartOffset;

    private bool _hasDraggedSignificantly;

    public MainView()
    {
        InitializeComponent();
        SetupRoomsDragScroll();
    }

    private void SetupRoomsDragScroll()
    {
        if (RoomsScrollViewer != null)
        {
            RoomsScrollViewer.AddHandler(InputElement.PointerPressedEvent, OnRoomsPointerPressed, RoutingStrategies.Tunnel);
            RoomsScrollViewer.AddHandler(InputElement.PointerMovedEvent, OnRoomsPointerMoved, RoutingStrategies.Tunnel);
            RoomsScrollViewer.AddHandler(InputElement.PointerReleasedEvent, OnRoomsPointerReleased, RoutingStrategies.Tunnel);
            RoomsScrollViewer.AddHandler(InputElement.PointerCaptureLostEvent, OnRoomsPointerCaptureLost, RoutingStrategies.Tunnel);
        }
    }

    private void OnRoomsPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (RoomsScrollViewer == null) return;
        var props = e.GetCurrentPoint(RoomsScrollViewer).Properties;
        if (props.IsLeftButtonPressed)
        {
            _isDraggingRooms = true;
            _hasDraggedSignificantly = false;
            _roomsDragStartPoint = e.GetPosition(RoomsScrollViewer);
            _roomsDragStartOffset = RoomsScrollViewer.Offset;
        }
    }

    private void OnRoomsPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDraggingRooms || RoomsScrollViewer == null) return;

        var currentPoint = e.GetPosition(RoomsScrollViewer);
        var deltaX = _roomsDragStartPoint.X - currentPoint.X;

        if (!_hasDraggedSignificantly && Math.Abs(deltaX) > 6)
        {
            _hasDraggedSignificantly = true;
            e.Pointer.Capture(RoomsScrollViewer);
        }

        if (_hasDraggedSignificantly)
        {
            var targetOffsetX = Math.Max(0, _roomsDragStartOffset.X + deltaX);
            RoomsScrollViewer.Offset = new Vector(targetOffsetX, RoomsScrollViewer.Offset.Y);
            e.Handled = true;
        }
    }

    private void OnRoomsPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDraggingRooms && RoomsScrollViewer != null)
        {
            _isDraggingRooms = false;
            if (_hasDraggedSignificantly)
            {
                e.Handled = true;
            }
            e.Pointer.Capture(null);
            _hasDraggedSignificantly = false;
        }
    }

    private void OnRoomsPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isDraggingRooms = false;
        _hasDraggedSignificantly = false;
    }

    private void OnScrollLeftClicked(object? sender, RoutedEventArgs e)
    {
        if (RoomsScrollViewer != null)
        {
            var newX = Math.Max(0, RoomsScrollViewer.Offset.X - 180);
            RoomsScrollViewer.Offset = new Vector(newX, RoomsScrollViewer.Offset.Y);
        }
    }

    private void OnScrollRightClicked(object? sender, RoutedEventArgs e)
    {
        if (RoomsScrollViewer != null)
        {
            var newX = RoomsScrollViewer.Offset.X + 180;
            RoomsScrollViewer.Offset = new Vector(newX, RoomsScrollViewer.Offset.Y);
        }
    }

    private void OnHomeItemClicked(object? sender, RoutedEventArgs e)
    {
        if (HomeSwitcherButton?.Flyout != null)
        {
            HomeSwitcherButton.Flyout.Hide();
        }
    }
}
