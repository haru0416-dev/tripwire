Pick the variable that receives the result in **Variable**, put a variable or a value in **A** and **B**, and choose `+ − × ÷ %` in **Operator**. The A and B fields take the variable's type.

It works on number (whole and decimal), position (Vector2, Vector3), color and text variables. Positions and colors add and subtract their own type, and `× ÷` multiply or divide them by a number: adding (0, 1, 0) to a position moves it 1 m up, and multiplying a color by 0.5 darkens it. Text can only be joined with `+`. `%` is the remainder, for numbers only.

Dividing a whole number by 0 is an error in Udon that stops the whole trigger, so a division by 0 gives 0 instead. A 0 typed straight into B is an error before applying.

Into a synced variable, the value reaches everyone and On Variable Changed runs, as with any variable change. To add once, **Add To Variable** does the same.
