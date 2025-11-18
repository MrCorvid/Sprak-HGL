using System;
namespace ProgrammingLanguageNr1
{
	public class AST_ArrayEndSignal : AST
	{
		public AST_ArrayEndSignal(Token token) : base(token) 
		{
			
		}
		
		public int ArraySize {
			get {
				return this.m_arraySize;
			}
			set {
				m_arraySize = value;
			}
		}

		public override AST Clone()
		{
			var clone = (AST_ArrayEndSignal)base.Clone();
			clone.m_arraySize = this.m_arraySize;
			return clone;
		}
		
		private int m_arraySize;
	}
}