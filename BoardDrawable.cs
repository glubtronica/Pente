using Microsoft.Maui.Graphics;

namespace MultiPente;

public bool ShadowsEnabled { get; set; } = true;
public bool LastMoveHighlightEnabled { get; set; } = true;
public sealed class BoardDrawable : IDrawable
{
    public GameEngine Engine { get; }
    public Func<(float offsetX, float offsetY, float scale)> GetView { get; }

    // World layout
    public float Cell = 42f;     // distance between intersections in world units
    public float Margin = 40f;   // extra space around the grid in world units

    public BoardDrawable(GameEngine engine, Func<(float offsetX, float offsetY, float scale)> getView)
    {
        Engine = engine;
        GetView = getView;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var (ox, oy, scale) = GetView();

        canvas.SaveState();

        // Background
        canvas.FillColor = Colors.FromArgb("#121417");
        canvas.FillRectangle(dirtyRect);

        // Apply camera transform (screen space)
        canvas.Translate(ox, oy);
        canvas.Scale(scale, scale);

        // Board bounds in world space
        float gridSize = (Engine.Size - 1) * Cell;
        var boardRect = new RectF(Margin, Margin, gridSize, gridSize);

        // Board "wood" panel
        canvas.FillColor = Colors.FromArgb("#2B2F36");
        canvas.FillRoundedRectangle(boardRect.Inflate(28, 28), 20);

        // Grid
        canvas.StrokeColor = Colors.FromArgb("#C7CDD6");
        canvas.StrokeSize = 1.5f / scale; // keep line thickness stable-ish

        for (int i = 0; i < Engine.Size; i++)
        {
            float x = Margin + i * Cell;
            float y = Margin + i * Cell;

            // vertical
            canvas.DrawLine(x, Margin, x, Margin + gridSize);
            // horizontal
            canvas.DrawLine(Margin, y, Margin + gridSize, y);
        }

        // Star points (like Go) for 19x19
        if (Engine.Size == 19)
        {
            int[] pts = { 3, 9, 15 };
            canvas.FillColor = Colors.FromArgb("#C7CDD6");
            float r = 3.2f / scale;
            foreach (var px in pts)
                foreach (var py in pts)
                    canvas.FillCircle(WorldX(px), WorldY(py), r);
        }

        // Last move highlight
        if (Engine.LastMove is { } lm)
        {
            float cx = WorldX(lm.x);
            float cy = WorldY(lm.y);
            canvas.StrokeColor = Colors.Yellow;
            canvas.StrokeSize = 2.5f / scale;
            canvas.DrawCircle(cx, cy, 18f);
        }

        // Stones
        for (int y = 0; y < Engine.Size; y++)
        {
            for (int x = 0; x < Engine.Size; x++)
            {
                char t = Engine.Board[x, y];
                if (t == '\0') continue;

                float cx = WorldX(x);
                float cy = WorldY(y);
                DrawStone(canvas, t, cx, cy, 16f, scale);
            }
        }

        canvas.RestoreState();
    }

    private float WorldX(int x) => Margin + x * Cell;
    private float WorldY(int y) => Margin + y * Cell;

    private static void DrawStone(ICanvas canvas, char token, float cx, float cy, float radius, float scale)
    {
        // Simple palette
        Color fill = token switch
        {
            'G' => Colors.FromArgb("#34D399"), // green
            'B' => Colors.FromArgb("#60A5FA"), // blue
            'W' => Colors.FromArgb("#F9FAFB"), // white
            'K' => Colors.FromArgb("#111827"), // black
            _ => Colors.Magenta
        };

        canvas.FillColor = fill;

        // subtle shadow
        canvas.SaveState();
        canvas.FillColor = Colors.Black.WithAlpha(0.25f);
        canvas.FillCircle(cx + 2.2f / scale, cy + 2.2f / scale, radius);
        canvas.RestoreState();

        // fill
        canvas.FillCircle(cx, cy, radius);

        // outline
        canvas.StrokeColor = (token == 'W') ? Colors.FromArgb("#111827") : Colors.White.WithAlpha(0.35f);
        canvas.StrokeSize = 2.0f / scale;
        canvas.DrawCircle(cx, cy, radius);
    }
}