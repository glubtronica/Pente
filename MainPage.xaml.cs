using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;

namespace MultiPente;

public partial class MainPage : ContentPage
{
    private readonly GameEngine _engine;

    // "Camera" transform in screen space
    private float _offsetX = 0;
    private float _offsetY = 0;
    private float _scale = 1.0f;

    // Interaction state
    private bool _isPinching;
    private bool _isPanning;
    private PointF _startPointScreen;
    private PointF _lastPointScreen;
    private float _pinchStartDistance;
    private float _pinchStartScale;
    private PointF _pinchStartMidScreen;
    private float _startOffsetX, _startOffsetY;

    private DateTime _touchStartTime;

    // Tunables
    private const float MinScale = 0.55f;
    private const float MaxScale = 2.8f;
    private const float TapMoveThreshold = 10f; // px

    public MainPage()
    {
        InitializeComponent();

        // Start with 4 players to match your usual mode (change to 2–4)
        _engine = new GameEngine(playerCount: 4);

        var drawable = new BoardDrawable(_engine, () => (_offsetX, _offsetY, _scale));
        BoardView.Drawable = drawable;

        _engine.StateChanged += () =>
        {
            RefreshHud();
            BoardView.Invalidate();
        };

        // Center the board on first layout
        BoardView.SizeChanged += (_, __) => CenterBoardIfNeeded(drawable);

        RefreshHud();
    }

    private void CenterBoardIfNeeded(BoardDrawable drawable)
    {
        if (BoardView.Width <= 0 || BoardView.Height <= 0) return;

        // If user already moved/zoomed, don’t override.
        if (Math.Abs(_offsetX) > 0.01f || Math.Abs(_offsetY) > 0.01f || Math.Abs(_scale - 1f) > 0.01f)
            return;

        // Compute board world bounds
        float gridSize = (_engine.Size - 1) * drawable.Cell;
        float boardWorldW = gridSize + (drawable.Margin * 2);
        float boardWorldH = gridSize + (drawable.Margin * 2);

        // Fit-to-view-ish
        float sx = (float)(BoardView.Width / boardWorldW);
        float sy = (float)(BoardView.Height / boardWorldH);
        _scale = Clamp(MathF.Min(sx, sy) * 0.95f, MinScale, MaxScale);

        // Center
        float worldCenterX = boardWorldW / 2f;
        float worldCenterY = boardWorldH / 2f;

        float viewCenterX = (float)BoardView.Width / 2f;
        float viewCenterY = (float)BoardView.Height / 2f;

        _offsetX = viewCenterX - worldCenterX * _scale;
        _offsetY = viewCenterY - worldCenterY * _scale;

        BoardView.Invalidate();
    }

    private void RefreshHud()
    {
        var p = _engine.CurrentPlayer;
        TurnLabel.Text = _engine.IsGameOver ? "Game Over" : $"Turn: {p.Name} ({p.Token})";

        // show capture counts
        var parts = new List<string>();
        foreach (var pl in _engine.Players)
            parts.Add($"{pl.Token}:{_engine.GetPairs(pl.Token)}/{_engine.PairsToWin}");
        ScoreLabel.Text = "Captures • " + string.Join("  ", parts);

        StatusLabel.Text = _engine.WinnerMessage ?? "";
    }

    private void Reset_Clicked(object sender, EventArgs e)
    {
        _engine.Reset();
    }

    private void Undo_Clicked(object sender, EventArgs e)
    {
        _engine.Undo();
    }

    // Touch handling (tap/pan/pinch) via GraphicsView interactions
    private void BoardView_StartInteraction(object sender, TouchEventArgs e)
    {
        if (e.Touches.Count == 0) return;

        _touchStartTime = DateTime.UtcNow;

        if (e.Touches.Count == 1)
        {
            _isPinching = false;
            _isPanning = true;

            _startPointScreen = ToPointF(e.Touches[0]);
            _lastPointScreen = _startPointScreen;

            _startOffsetX = _offsetX;
            _startOffsetY = _offsetY;
        }
        else if (e.Touches.Count >= 2)
        {
            _isPinching = true;
            _isPanning = false;

            var p1 = ToPointF(e.Touches[0]);
            var p2 = ToPointF(e.Touches[1]);

            _pinchStartDistance = Distance(p1, p2);
            _pinchStartScale = _scale;
            _pinchStartMidScreen = Midpoint(p1, p2);

            _startOffsetX = _offsetX;
            _startOffsetY = _offsetY;
        }
    }

    private void BoardView_DragInteraction(object sender, TouchEventArgs e)
    {
        if (e.Touches.Count == 0) return;

        if (_isPinching && e.Touches.Count >= 2)
        {
            var p1 = ToPointF(e.Touches[0]);
            var p2 = ToPointF(e.Touches[1]);

            float dist = MathF.Max(1f, Distance(p1, p2));
            float factor = dist / MathF.Max(1f, _pinchStartDistance);

            float newScale = Clamp(_pinchStartScale * factor, MinScale, MaxScale);

            // Zoom around pinch midpoint (keep midpoint stable)
            var mid = Midpoint(p1, p2);

            // world point under the midpoint BEFORE zoom
            var worldBefore = ScreenToWorld(mid, _startOffsetX, _startOffsetY, _pinchStartScale);

            _scale = newScale;

            // adjust offset so that same world point stays under current midpoint
            _offsetX = mid.X - worldBefore.X * _scale;
            _offsetY = mid.Y - worldBefore.Y * _scale;

            BoardView.Invalidate();
            return;
        }

        if (_isPanning && e.Touches.Count == 1)
        {
            var current = ToPointF(e.Touches[0]);
            var delta = new PointF(current.X - _lastPointScreen.X, current.Y - _lastPointScreen.Y);

            _offsetX += delta.X;
            _offsetY += delta.Y;

            _lastPointScreen = current;

            BoardView.Invalidate();
        }
    }

    private void BoardView_EndInteraction(object sender, TouchEventArgs e)
    {
        // If it was a pinch, don’t place.
        if (_isPinching)
        {
            _isPinching = false;
            return;
        }

        if (!_isPanning) return;

        _isPanning = false;

        // Treat as tap if movement was small
        float moved = Distance(_startPointScreen, _lastPointScreen);
        if (moved > TapMoveThreshold) return;

        // Convert tap point to board intersection
        var tapScreen = _startPointScreen;
        var tapWorld = ScreenToWorld(tapScreen, _offsetX, _offsetY, _scale);

        if (TryWorldToBoard(tapWorld, out int bx, out int by))
        {
            _engine.TryPlace(bx, by, out _);
        }
    }

    // --- Coordinate conversions ---
    private bool TryWorldToBoard(PointF world, out int bx, out int by)
    {
        // Must match BoardDrawable layout
        // (Keep these in sync: Margin/Cell)
        const float cell = 42f;
        const float margin = 40f;

        float fx = (world.X - margin) / cell;
        float fy = (world.Y - margin) / cell;

        // nearest intersection
        int ix = (int)MathF.Round(fx);
        int iy = (int)MathF.Round(fy);

        bx = ix;
        by = iy;

        if (!_engine.InBounds(ix, iy)) return false;

        // Also require tap reasonably close to intersection
        float wx = margin + ix * cell;
        float wy = margin + iy * cell;

        float dist = Distance(world, new PointF(wx, wy));
        return dist <= (cell * 0.45f);
    }

    private static PointF ScreenToWorld(PointF screen, float ox, float oy, float scale)
        => new((screen.X - ox) / scale, (screen.Y - oy) / scale);

    private static float Clamp(float v, float min, float max) => MathF.Max(min, MathF.Min(max, v));

    private static float Distance(PointF a, PointF b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static PointF Midpoint(PointF a, PointF b) => new((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f);

    private static PointF ToPointF(Point touchPoint) => new((float)touchPoint.X, (float)touchPoint.Y);
}