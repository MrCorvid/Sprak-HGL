// ProgrammingLanguageNr1/AST_Variable.cs

namespace ProgrammingLanguageNr1
{
    public class AST_Variable : AST
    {
        public AST_Variable(Token token) : base(token)
        {
            // A variable usage is represented by its NAME token.
        }

        public override string ToString()
        {
            return "var:" + getTokenString();
        }

		public override AST Clone()
		{
			return (AST_Variable)base.Clone();
		}
    }
}