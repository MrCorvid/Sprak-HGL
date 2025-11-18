using System;

namespace ProgrammingLanguageNr1
{
	// For calling methods on objects:
	// radio.PlaySound()

	public class AST_MethodCall : AST
	{
		public AST_MethodCall ()
		{
		}

		public override AST Clone()
		{
			return (AST_MethodCall)base.Clone();
		}
	}
}