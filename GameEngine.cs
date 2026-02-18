using System;
using System.Collections.Generic;

namespace MultiPente;

public sealed class GameEngine
{
    public const int DefaultSize = 19;
    public const int DefaultPairsToWin = 5;
    public const int DefaultInRowToWin = 5;

    public int Size { get; }
    public int PairsToWin { get; }
    public int InRowToWin { get; }

    // Board stores player tokens like 'G','B','W','K' or '\0' for empty.
    public char[,] Board { get; }

    public IReadOnlyList<Player> Players => _players;
    public int CurrentPlayerIndex { get; private set; }
    public Player CurrentPlayer => _players[CurrentPlayerIndex];

    public (int x, int y)? LastMove { get; private set; }

    public bool IsGameOver { get; private set; }
    public string? WinnerMessage { get; private set; }

    public event Action? StateChanged;

    private readonly List<Player> _players;
    private readonly Dictionary<char, int> _capturedPairs = new();
    private readonly Stack<MoveRecord> _history = new();

    private static readonly (int dx, int dy)[] Directions8 =
    {
        ( 1, 0), (-1, 0),
        ( 0, 1), ( 0,-1),
        ( 1, 1), (-1,-1),
        ( 1,-1), (-1, 1),
    };

    private static readonly (int dx, int dy)[] Axes4 =
    {
        ( 1, 0),
        ( 0, 1),
        ( 1, 1),
        ( 1,-1),
    };

    public GameEngine(int playerCount = 2, int size = DefaultSize, int pairsToWin = DefaultPairsToWin, int inRowToWin = DefaultInRowToWin)
    {
        if (playerCount is < 2 or > 4) throw new ArgumentOutOfRangeException(nameof(playerCount));
        if (size < 5) throw new ArgumentOutOfRangeException(nameof(size));

        Size = size;
        PairsToWin = pairsToWin;
        InRowToWin = inRowToWin;

        Board = new char[size, size];

        char[] tokens = { 'G', 'B', 'W', 'K' };
        _players = new List<Player>(playerCount);
        for (int i = 0; i < playerCount; i++)
            _players.Add(new Player($"P{i + 1}", tokens[i]));

        foreach (var p in _players)
            _capturedPairs[p.Token] = 0;

        CurrentPlayerIndex = 0;
    }

    public int GetPairs(char token) => _capturedPairs.TryGetValue(token, out var v) ? v : 0;

public void SetPlayerName(int index, string name)
{
    if (index < 0 || index >= _players.Count) return;
    name = (name ?? "").Trim();
    if (string.IsNullOrWhiteSpace(name)) name = $"P{index + 1}";

    // Replace record (since Player is a record)
    _players[index] = _players[index] with { Name = name };
    StateChanged?.Invoke();
}
    public void Reset()
    {
        Array.Clear(Board, 0, Board.Length);
        _history.Clear();
        foreach (var p in _players) _capturedPairs[p.Token] = 0;

        CurrentPlayerIndex = 0;
        LastMove = null;
        IsGameOver = false;
        WinnerMessage = null;

        StateChanged?.Invoke();
    }

    public bool CanUndo => _history.Count > 0;

    public void Undo()
    {
        if (!CanUndo) return;

        var rec = _history.Pop();

        // restore player turn
        CurrentPlayerIndex = rec.PlayerIndex;

        // remove placed stone
        Board[rec.X, rec.Y] = '\0';

        // restore captured stones
        foreach (var c in rec.Captured)
            Board[c.x, c.y] = c.token;

        // revert capture count
        _capturedPairs[rec.PlayerToken] -= rec.PairsCaptured;

        LastMove = _history.Count > 0 ? (_history.Peek().X, _history.Peek().Y) : null;

        IsGameOver = false;
        WinnerMessage = null;

        StateChanged?.Invoke();
    }

    public bool InBounds(int x, int y) => x >= 0 && x < Size && y >= 0 && y < Size;

    public bool TryPlace(int x, int y, out int pairsCapturedThisMove)
    {
        pairsCapturedThisMove = 0;

        if (IsGameOver) return false;
        if (!InBounds(x, y)) return false;
        if (Board[x, y] != '\0') return false;

        var me = CurrentPlayer.Token;

        // Place
        Board[x, y] = me;

        // Capture
        var captured = new List<(int x, int y, char token)>();
        int pairsCaptured = ApplyMixedPairCaptures(x, y, me, captured);

        // Record history for undo
        _history.Push(new MoveRecord(
            PlayerIndex: CurrentPlayerIndex,
            PlayerToken: me,
            X: x, Y: y,
            PairsCaptured: pairsCaptured,
            Captured: captured
        ));

        pairsCapturedThisMove = pairsCaptured;

        LastMove = (x, y);

        // Check wins
        if (GetPairs(me) >= PairsToWin)
        {
            IsGameOver = true;
            WinnerMessage = $"{CurrentPlayer.Name} ({me}) wins by captures ({GetPairs(me)}/{PairsToWin})!";
        }
        else if (HasNInRow(x, y, me, InRowToWin))
        {
            IsGameOver = true;
            WinnerMessage = $"{CurrentPlayer.Name} ({me}) wins by {InRowToWin}-in-a-row!";
        }

        // Next turn only if game continues
        if (!IsGameOver)
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % _players.Count;

        StateChanged?.Invoke();
        return true;
    }

    private int ApplyMixedPairCaptures(int x, int y, char me, List<(int x, int y, char token)> capturedOut)
    {
        // Variant: A B C A captures B and C even if B/C are different colors.
        int pairs = 0;

        foreach (var (dx, dy) in Directions8)
        {
            int x1 = x + dx, y1 = y + dy;
            int x2 = x + 2 * dx, y2 = y + 2 * dy;
            int x3 = x + 3 * dx, y3 = y + 3 * dy;

            if (!InBounds(x3, y3)) continue;

            char s1 = Board[x1, y1];
            char s2 = Board[x2, y2];
            char s3 = Board[x3, y3];

            if (s3 != me) continue;
            if (s1 == '\0' || s2 == '\0') continue;
            if (s1 == me || s2 == me) continue;

            // capture
            capturedOut.Add((x1, y1, s1));
            capturedOut.Add((x2, y2, s2));

            Board[x1, y1] = '\0';
            Board[x2, y2] = '\0';

            _capturedPairs[me] = GetPairs(me) + 1;
            pairs++;
        }

        return pairs;
    }

    private bool HasNInRow(int x, int y, char me, int n)
    {
        foreach (var (dx, dy) in Axes4)
        {
            int count = 1;
            count += CountDir(x, y, dx, dy, me);
            count += CountDir(x, y, -dx, -dy, me);
            if (count >= n) return true;
        }
        return false;
    }

    private int CountDir(int x, int y, int dx, int dy, char me)
    {
        int c = 0;
        int cx = x + dx, cy = y + dy;
        while (InBounds(cx, cy) && Board[cx, cy] == me)
        {
            c++;
            cx += dx;
            cy += dy;
        }
        return c;
    }

    public sealed record Player(string Name, char Token);

    private sealed record MoveRecord(
        int PlayerIndex,
        char PlayerToken,
        int X,
        int Y,
        int PairsCaptured,
        List<(int x, int y, char token)> Captured
    );
}