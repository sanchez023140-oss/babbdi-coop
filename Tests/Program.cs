using BabbdiCoop;

// v0.10.44: offline checks for SnakeTurn — the one turn rule shared by the solo Zmejka
// cabinet and the duel cabinet. No Unity, no game, runs in a second.
//
// Background: v0.10.42 shipped a bug where right -> up -> left inside one move tick was
// accepted and the head stepped into its own neck (a straight snake, instant death).
// These cases lock that behaviour down so it cannot come back.

int failed = 0;

void Check(string name, bool ok, string detail = "")
{
    Console.WriteLine($"{(ok ? "  ok  " : " FAIL ")} {name}{(detail.Length > 0 ? "  [" + detail + "]" : "")}");
    if (!ok) failed++;
}

// --- 1. a normal turn is accepted and buffered --------------------------------
{
    int qx = 99, qy = 99;
    bool has = false;
    bool accepted = SnakeTurn.Accept(1, 0, ref qx, ref qy, ref has, 0, -1);
    Check("moving right, up is accepted", accepted && has && qx == 0 && qy == -1,
          $"accepted={accepted} has={has} queued=({qx},{qy})");
}

// --- 2. THE BUG: a second turn in the same tick must not reverse the snake ----
{
    int qx = 99, qy = 99;
    bool has = false;
    SnakeTurn.Accept(1, 0, ref qx, ref qy, ref has, 0, -1);          // up buffered
    bool accepted = SnakeTurn.Accept(1, 0, ref qx, ref qy, ref has, -1, 0); // left, same tick
    Check("same-tick left after up is rejected", !accepted && has && qx == 0 && qy == -1,
          $"accepted={accepted} has={has} queued=({qx},{qy})");
    Check("the rejected press does not clear the buffered turn", has && qx == 0 && qy == -1);
}

// --- 3. pressing the current direction cancels the buffered turn --------------
{
    int qx = 99, qy = 99;
    bool has = false;
    SnakeTurn.Accept(1, 0, ref qx, ref qy, ref has, 0, -1);
    bool accepted = SnakeTurn.Accept(1, 0, ref qx, ref qy, ref has, 1, 0);
    Check("straight on cancels the buffer", !accepted && !has);
}

// --- 4. a bare reverse is always rejected -------------------------------------
{
    int qx = 99, qy = 99;
    bool has = false;
    bool accepted = SnakeTurn.Accept(0, -1, ref qx, ref qy, ref has, 0, 1);
    Check("moving up, down is rejected", !accepted && !has);
}

// --- 5. the same turn one step later is legal (a two-cell U-turn) -------------
{
    int qx = 0, qy = 0;
    bool has = false;
    SnakeTurn.Accept(0, -1, ref qx, ref qy, ref has, -1, 0);   // now moving up, turn left
    Check("one step later left is accepted", has && qx == -1 && qy == 0, $"queued=({qx},{qy})");
}

// --- 6. the buffer can be replaced by another legal turn ----------------------
{
    int qx = 99, qy = 99;
    bool has = false;
    SnakeTurn.Accept(1, 0, ref qx, ref qy, ref has, 0, -1);    // up
    bool accepted = SnakeTurn.Accept(1, 0, ref qx, ref qy, ref has, 0, 1); // changed my mind: down
    Check("a buffered turn can be replaced by a legal one", accepted && qx == 0 && qy == 1,
          $"queued=({qx},{qy})");
}

// --- 7. the full path of the reported burst, simulated step by step -----------
//     body: (6,8) (5,8) (4,8) (3,8), moving right. The player presses up, then left,
//     then the snake steps. With the bug the step went to (5,8) = its own neck.
{
    var body = new List<(int x, int y)> { (6, 8), (5, 8), (4, 8), (3, 8) };
    int dx = 1, dy = 0, qx = 0, qy = 0;
    bool has = false;

    SnakeTurn.Accept(dx, dy, ref qx, ref qy, ref has, 0, -1);
    SnakeTurn.Accept(dx, dy, ref qx, ref qy, ref has, -1, 0);

    if (has) { dx = qx; dy = qy; has = false; }
    var next = (x: body[0].x + dx, y: body[0].y + dy);
    bool intoNeck = next == body[1];
    Check("the burst steps UP, not into the neck", next == (6, 7) && !intoNeck,
          $"head={body[0]} next={next} neck={body[1]}");
}

Console.WriteLine();
Console.WriteLine(failed == 0 ? "ALL CHECKS PASSED" : $"{failed} CHECK(S) FAILED");
return failed == 0 ? 0 : 1;
