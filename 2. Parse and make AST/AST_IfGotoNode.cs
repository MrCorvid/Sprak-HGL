namespace ProgrammingLanguageNr1
{
    /// <summary>
    /// Represents a generic conditional GOTO.
    /// The interpreter will first execute its single child (the condition),
    /// then check the resulting boolean on the value stack. If true, it jumps.
    /// </summary>
    public class AST_IfGotoNode : AST
    {
        public readonly string TargetLabel;

        public AST_IfGotoNode(Token token, string targetLabel) : base(token)
        {
            this.TargetLabel = targetLabel;
        }
    }
}