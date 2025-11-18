//#define WRITE_DEBUG_INFO
//#define PRINT_STACK
//#define WRITE_CONVERT_INFO
#define LOG_SCOPES

#define BUILT_IN_PROFILING

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ProgrammingLanguageNr1
{
    public class InterpreterTwo : IEnumerable<InterpreterTwo.Status>
    {
        public enum Status {
            OK,
            ERROR,
            FINISHED
        }

		public static int nrOfInterpreters = 0;

        public Action<string>? OnTrace;

        private void Trace(Func<string> messageBuilder)
        {
            OnTrace?.Invoke(messageBuilder());
        }

		public InterpreterTwo(AST ast, Scope globalScope, ErrorHandler errorHandler, ExternalFunctionCreator externalFunctionCreator)
        {
			#if WRITE_DEBUG_INFO
            	Console.WriteLine("\nCreated Interpreter!!");
			#endif
			
            m_ast = ast; 
            m_errorHandler = errorHandler;
            m_globalScope = globalScope;
            m_currentScope = m_globalScope;
            m_externalFunctionCreator = externalFunctionCreator;
            Reset();

			nrOfInterpreters++;
        }

		~InterpreterTwo() {
			nrOfInterpreters--;
		}

        public void Reset()
        {
			#if BUILT_IN_PROFILING
			m_profileData.Clear ();
			#endif

			if(m_globalMemorySpace != null) {
				m_globalMemorySpace.Delete();
				m_globalMemorySpace = null;
			}
			if(m_currentMemorySpace != null) {
				m_currentMemorySpace.Delete();
				m_currentMemorySpace = null;
			}

			if(m_globalScope != null) {
				m_globalScope.ClearMemorySpaces();
			}

			if(m_currentScope != null) {
				m_currentScope.ClearMemorySpaces();
			}

			m_valueStack.Clear();
			m_memorySpaceStack.Clear ();
            
			m_memorySpaceNodeListCache.clear();

			m_globalMemorySpace = new MemorySpace("globals", m_ast.getChild (0), m_globalScope, m_memorySpaceNodeListCache);
			m_currentMemorySpace = m_globalMemorySpace;

			m_currentScope = m_globalScope;
			m_currentScope.ClearMemorySpaces();
			m_currentScope.PushMemorySpace(m_currentMemorySpace);

			m_topLevelDepth = 0;
        }

        public IEnumerator<Status> GetEnumerator()
        {
            while (ExecuteNextStatement())
            {
                yield return Status.OK;
            }
			if(m_errorHandler.getErrors().Count > 0) {
				yield return Status.ERROR;
			}
			else {
            	yield return Status.FINISHED;
			}
        }

        private void PushNewScope(Scope newScope, string nameOfNewMemorySpace, AST startNode) {
			#if DEBUG
			Debug.Assert(newScope != null);
			Debug.Assert(startNode != null);
			#endif

			if (m_memorySpaceStack.Count > 100) {
				var token = startNode.getToken ();
				throw new Error ("Stack overflow!", Error.ErrorType.RUNTIME, token.LineNr, token.LinePosition);
			}

            m_currentScope = newScope;
            m_memorySpaceStack.Push(m_currentMemorySpace);
            
            m_currentMemorySpace = new MemorySpace(nameOfNewMemorySpace, startNode, m_currentScope, m_memorySpaceNodeListCache);
			m_currentScope.PushMemorySpace(m_currentMemorySpace);

#if LOG_SCOPES
			nrOfScopes++;
#endif
        }

		public static int nrOfScopes;

        private void PopCurrentScope()
        {
            var poppedMemorySpace = m_currentScope.PopMemorySpace();

            m_currentMemorySpace = m_memorySpaceStack.Pop();
            m_currentScope = m_currentMemorySpace.Scope;
            m_currentScope.PushMemorySpace(m_currentMemorySpace);
#if LOG_SCOPES
			nrOfScopes--;
#endif
        }

        public void PrintValueStack()
        {
            Console.Write("VALUE_STACK: ");
            foreach (object rv in m_valueStack)
            {
                Console.Write(rv.ToString() + ", ");
            }
            Console.Write("\n");
        }

        public void PrintMemoryStack()
        {
            Console.WriteLine("MEMORY_STACK:");
            Console.WriteLine("\t" + m_currentMemorySpace.getName() + ":");
            m_currentMemorySpace.PrintValues();
            foreach (MemorySpace m in m_memorySpaceStack)
            {
                Console.WriteLine("\t" + m.getName() + ":");
                m.PrintValues();
            }
        }
		
        public void SwapStackTopValueTo(object pValue)
        {
			if(m_valueStack.Count > 0) {
            	m_valueStack.Pop();
            	m_valueStack.Push(pValue);
			} else {
				System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
				Console.WriteLine("SwapStackTopValueTo '" + pValue + "' but stack is empty, stacktrace: " + t.ToString());
				throw new Error("Can't return value (stack empty)");
			}
        }

		public bool HasFunction(string functionName) {
			return m_globalScope.resolve(functionName) != null;
		}

		public enum ProgramFunctionCallStatus {
			NO_FUNCTION,
			EXTERNAL_FUNCTION,
			NORMAL_FUNCTION,
		};

		public ProgramFunctionCallStatus SetProgramToExecuteFunction (string functionName, object[] args)
		{
			FunctionSymbol functionSymbol = (FunctionSymbol)m_globalScope.resolve(functionName);

			if(functionSymbol == null) {
				return ProgramFunctionCallStatus.NO_FUNCTION;
			}

			if (IsFunctionExternal(functionName)) {
				CallExternalFunction(functionName, args);
				return ProgramFunctionCallStatus.EXTERNAL_FUNCTION;
			} else {
				AST_FunctionDefinitionNode functionDefinitionNode = (AST_FunctionDefinitionNode)functionSymbol.getFunctionDefinitionNode();

				if (functionDefinitionNode != null) {

					var parameters = functionDefinitionNode.getChild(2).getChildren();
					int nrOfParameters = parameters.Count;
					if (nrOfParameters != args.Length) {
						throw new Error ("The function " + functionName + " takes " + nrOfParameters + " arguments, not " + args.Length);
					}

					Reset();

					m_topLevelDepth = 1;

					m_currentScope = functionDefinitionNode.getScope();
					m_currentScope.ClearMemorySpaces();

					string nameOfNewMemorySpace = functionName + "_memorySpace" + functionCounter++;
					PushNewScope(functionDefinitionNode.getScope(), nameOfNewMemorySpace, functionDefinitionNode);

					for (int i = args.Length - 1; i >= 0; i--) {
						var declaration = parameters[i].getChild(0) as AST_VariableDeclaration;
						object convertedValue = ReturnValueConversions.ChangeTypeBasedOnReturnValueType(args[i], declaration.Type);
						PushValue(convertedValue); // reverse order
					}
				} else {
					throw new Error(functionName + " has got no function definition node!");
				}
			}

			return ProgramFunctionCallStatus.NORMAL_FUNCTION;
		}
			
		public object GetGlobalVariableValue(string pName) 
		{
            return m_globalMemorySpace.getValue(pName);
		}

        private bool ExecuteNextStatement() {
			#if DEBUG
            Debug.Assert(m_currentMemorySpace != null);
			#endif

            while (!m_currentMemorySpace.Next())
            {
				if (m_memorySpaceStack.Count == m_topLevelDepth)
                {
                    return false;
                }
                else
                {
                    PopCurrentScope();
                }
            }
        
#if WRITE_DEBUG_INFO
			Console.WriteLine("\nExecuting " + CurrentNode.getTokenString());
#endif
			try {
				SwitchOnStatement();
			}
			catch(Error e) {
				if(e.getLineNr() < 0) {
					m_errorHandler.errorOccured(new Error(e.getMessage(), e.getErrorType(), CurrentNode.getToken().LineNr, CurrentNode.getToken().LinePosition));
				}
				else {
					m_errorHandler.errorOccured(e);
				}				
			}

            return true;
        }
		
		private void SwitchOnStatement() 
		{
			CurrentNode.Executions++;
			
            switch (CurrentNode.getTokenType())
            {
                case Token.TokenType.LABEL:
                    // A label is a destination for a GOTO. No action is needed when executing it sequentially.
                    break;

                case Token.TokenType.GOTO:
                    string labelName = CurrentNode.getTokenString();
                    m_currentMemorySpace.JumpToLabel(labelName);
                    break;
                
                case Token.TokenType.IF_GOTO:
                    HandleIfGoto();
                    break;

                case Token.TokenType.STATEMENT_LIST:
                case Token.TokenType.NODE_GROUP:
                case Token.TokenType.BUILT_IN_TYPE_NAME:
                    break;

                case Token.TokenType.IF:
                    EvaluateIf();
                    break;

                case Token.TokenType.PARAMETER:
                    break;

                case Token.TokenType.FUNCTION_CALL:
                    JumpToFunction();
                    break;

                case Token.TokenType.QUOTED_STRING:
                    PushValueFromToken();
                    break;

                case Token.TokenType.FUNC_DECLARATION:
                    throw new Exception("Can't happen: FUNC_DECLARATION should not be in the execution list.");

                case Token.TokenType.VAR_DECLARATION:
                    VariableDeclaration();
                    break;

                case Token.TokenType.ASSIGNMENT:
					AssignmentSignal();
                    break;
				
				case Token.TokenType.ASSIGNMENT_TO_ARRAY:
                    AssignmentToArrayElementSignal();
                    break;
				
				case Token.TokenType.ARRAY_END_SIGNAL:
                    ArrayEndSignal();
                    break;

                case Token.TokenType.NAME:
                    ResolveVariableName();
                    break;

                case Token.TokenType.NUMBER:
                    PushValueFromToken();
                    break;
				
				case Token.TokenType.ARRAY:
                    PushValueFromToken();
                    break;
				
				case Token.TokenType.BOOLEAN_VALUE:
                    PushValueFromToken();
                    break;

                case Token.TokenType.OPERATOR:
                    Operator();
                    break;

                case Token.TokenType.RETURN:
                    ReturnSignal();
                    break;

                case Token.TokenType.LOOP:
                    Loop();
                    break;
				
				case Token.TokenType.LOOP_BLOCK:
                    LoopBlock();
                    break;
				
				case Token.TokenType.GOTO_BEGINNING_OF_LOOP:
					GotoBeginningOfLoop();
					break;
	
                case Token.TokenType.BREAK:
                    BreakStatement();
                    break;
				
				case Token.TokenType.ARRAY_LOOKUP:
                    ArrayLookup();
                    break;

				case Token.TokenType.NOT:
					Not();
					break;

                default:
                    throw new Exception("Interpreter hasn't implemented support for token type " + m_currentMemorySpace.CurrentNode.getTokenType() + " yet!");
            }
		}

        private void HandleIfGoto()
        {
            var node = CurrentNode as AST_IfGotoNode;
            if (node == null) {
                throw new Error("Internal error: Expected an AST_IfGotoNode.");
            }

            // The condition expression was the child of this node and was already processed
            // by the interpreter's post-order traversal, so its result is on the stack.
            object conditionValue = PopValue();
            bool condition = ConvertToBool(conditionValue);

            Trace(() => $"IF_GOTO on condition '{conditionValue}' ({condition}). Jump taken: {condition}");

            // The primitive is simple: jump if the condition is true.
            if (condition)
            {
                m_currentMemorySpace.JumpToLabel(node.TargetLabel);
            }
            // If we don't jump, we do nothing. The main loop will automatically advance to the next instruction.
        }

        static int ifCounter = 0;

        private void EvaluateIf()
        {
            AST_IfNode ifnode = CurrentNode as AST_IfNode;
			#if DEBUG
            Debug.Assert(ifnode != null);
			#endif

            object r = PopValue();
			#if DEBUG
            Debug.Assert(r != null);
			#endif

            AST subNode = null;

			if (r.GetType() != typeof(bool) && r.GetType() != typeof(float)) {
				var token = ifnode.getToken ();
				throw new Error ("Can't use value " + r + " of type " + ReturnValueConversions.PrettyObjectType (r.GetType()) + " in if-statement", Error.ErrorType.RUNTIME, token.LineNr, token.LinePosition);
			}
            
            bool conditionResult = ConvertToBool(r);
            Trace(() => $"IF Condition Value: '{r}' ({r.GetType().Name}), Evaluated As: {conditionResult}");

			if (conditionResult)
            {
                subNode = ifnode.getChild(1);
            }
            else
            {
                if (ifnode.getChildren().Count == 3)
                {
                    subNode = ifnode.getChild(2);
                }
            }

            if (subNode != null)
            {
                PushNewScope(ifnode.getScope(), "IF_memorySpace" + ifCounter++, subNode);                
            }
        }

        private AST CurrentNode
        {
            get
            {
				#if DEBUG
                Debug.Assert(m_currentMemorySpace != null);
				#endif
                return m_currentMemorySpace.CurrentNode;
            }
        }

        static int functionCounter = 0;

		bool IsFunctionExternal(string pFunctionName)
		{
			return m_externalFunctionCreator.externalFunctions.ContainsKey(pFunctionName);
		}

		void CallExternalFunction(string pFunctionName, object[] pParameters)
		{
			ExternalFunctionCreator.OnFunctionCall fc = m_externalFunctionCreator.externalFunctions[pFunctionName];
			object rv = fc(pParameters);
			if (!(rv is VoidType)) {
				PushValue(rv);
			}
		}

        private void JumpToFunction()
        {
			AST_FunctionDefinitionNode functionDefinitionNode = (CurrentNode as AST_FunctionCall).FunctionDefinitionRef;
            string functionName = functionDefinitionNode.getChild(1).getTokenString();
			var parameterDefs = functionDefinitionNode.getChild(2).getChildren();

			int nrOfParameters = parameterDefs.Count;
            object[] parameters = new object[nrOfParameters];
            for (int i = nrOfParameters - 1; i >= 0; i--)
            {
				var paramDef = parameterDefs[i];
				var declaration = paramDef.getChild(0) as AST_VariableDeclaration;
				parameters[i] = ReturnValueConversions.ChangeTypeBasedOnReturnValueType(PopValue(), declaration.Type);
            }

			if (IsFunctionExternal(functionName)) {
				CallExternalFunction(functionName, parameters);
			} else {
				PushNewScope(functionDefinitionNode.getScope(), functionName + "_memorySpace" + functionCounter++, functionDefinitionNode);

				for (int i = nrOfParameters - 1; i >= 0; i--) {
					PushValue(parameters[i]); // reverse order
				}
			}
        }

		private float ConvertToNumber(object o) {
			if(o.GetType() == typeof(float)) {
				return (float)o;
			}
			else if(o.GetType() == typeof(int)) {
				return (float)(int)o;
			}
			else if(o.GetType() == typeof(string)) {
				float f = 0f;
				if(float.TryParse((string)o, out f)) {
					return f;
				}
			}

			throw new Error("Can't convert value " + o + " of type " + ReturnValueConversions.PrettyObjectType(o.GetType()) + " to number");
		}

		private bool ConvertToBool(object o) {
			if(o.GetType() == typeof(bool)) {
				return (bool)o;
			}
			else if(o.GetType() == typeof(float)) {
				return ((float)o != 0f);
			}
			else if(o.GetType() == typeof(int)) {
				return ((int)o != 0);
			}
			throw new Error("Can't convert value " + o + " of type " + ReturnValueConversions.PrettyObjectType(o.GetType()) + " to bool");
		}

        private void Operator()
		{
			object result;
			float rhs, lhs;
			string op = CurrentNode.getTokenString();

			switch (op)
			{
				case "+":
					result = AddStuffTogetherHack();
					break;

				case "-":
					rhs = ConvertToNumber(PopValue());
					lhs = ConvertToNumber(PopValue());
					result = lhs - rhs;
					Trace(() => $"OPERATOR: {lhs} - {rhs} -> {result}");
					break;

				case "*":
					rhs = ConvertToNumber(PopValue());
					lhs = ConvertToNumber(PopValue());
					result = lhs * rhs;
					Trace(() => $"OPERATOR: {lhs} * {rhs} -> {result}");
					break;

				case "/":
					rhs = ConvertToNumber(PopValue());
					if (rhs == 0f)
					{
						Trace(() => "OPERATOR /: Division by zero. Returning 0.");
						result = 0f;
					}
					else
					{
						lhs = ConvertToNumber(PopValue());
						result = lhs / rhs;
						Trace(() => $"OPERATOR: {lhs} / {rhs} -> {result}");
					}
					break;
					
				// --- FIX START: Implemented the Modulus Operator ---
				case "%":
					rhs = ConvertToNumber(PopValue());
					if (rhs == 0f)
					{
						Trace(() => "OPERATOR %: Division by zero. Returning 0.");
						result = 0f;
					}
					else
					{
						lhs = ConvertToNumber(PopValue());
						result = lhs % rhs;
						Trace(() => $"OPERATOR: {lhs} % {rhs} -> {result}");
					}
					break;
				// --- FIX END ---
					
				case "<":
					rhs = ConvertToNumber(PopValue());
					lhs = ConvertToNumber(PopValue());
					result = lhs < rhs;
					Trace(() => $"OPERATOR: {lhs} < {rhs} -> {result}");
					break;
				case ">":
					rhs = ConvertToNumber(PopValue());
					lhs = ConvertToNumber(PopValue());
					result = lhs > rhs;
					Trace(() => $"OPERATOR: {lhs} > {rhs} -> {result}");
					break;
				case ">=":
					rhs = ConvertToNumber(PopValue());
					lhs = ConvertToNumber(PopValue());
					result = lhs >= rhs;
					Trace(() => $"OPERATOR: {lhs} >= {rhs} -> {result}");
					break;
				case "<=":
					rhs = ConvertToNumber(PopValue());
					lhs = ConvertToNumber(PopValue());
					result = lhs <= rhs;
					Trace(() => $"OPERATOR: {lhs} <= {rhs} -> {result}");
					break;
				case "==":
					result = equalityTest();
					break;
				case "!=":
					result = !ConvertToBool(equalityTest());
					Trace(() => $"OPERATOR != -> {result}");
					break;
				case "&&":
					{
						object a = PopValue();
						bool a_bool = ConvertToBool(a);
						object b = PopValue();
						bool b_bool = ConvertToBool(b);
						result = a_bool && b_bool;
						Trace(() => $"OPERATOR &&: '{a}' && '{b}' -> {result}");
					}
					break;
				case "||":
					{
						object a2 = PopValue();
						bool a2_bool = ConvertToBool(a2);
						object b2 = PopValue();
						bool b2_bool = ConvertToBool(b2);
						result = a2_bool || b2_bool;
						Trace(() => $"OPERATOR ||: '{a2}' || '{b2}' -> {result}");
					}
					break;
				default:
					throw new Exception("Operator " + op + " is not implemented yet!");
			}
			
			PushValue(result);
		}
		
		private object equalityTest() {
			object rhs = PopValue();
            object lhs = PopValue();
            object result;

            var traceBuilder = new StringBuilder();
            traceBuilder.Append($"OPERATOR ==: '{lhs}' ({lhs.GetType().Name}) == '{rhs}' ({rhs.GetType().Name}) -> ");

			if (lhs == rhs) {
				result = true;
			}
			else if(lhs.GetType() == typeof(float) && lhs.GetType() == rhs.GetType()) {
				result = (((float)rhs) == ((float)lhs));
			}
			else if(lhs.GetType() == typeof(int) && lhs.GetType() == rhs.GetType()) {
				result = (((int)rhs) == ((int)lhs));
			}
			else if(lhs.GetType() == rhs.GetType() && rhs is IComparable && lhs is IComparable)
			{
				result = (rhs as IComparable).CompareTo(lhs as IComparable) == 0;
			}
			else {
			    result = false;
            }

            traceBuilder.Append(result);
            Trace(() => traceBuilder.ToString());
            return result;
		}
		
		private object AddStuffTogetherHack() {
		
			object rhs = PopValue();
			object lhs = PopValue();
            object result;
				
			var rightValueType = rhs.GetType ();
			var leftValueType = lhs.GetType ();

			if (rightValueType == typeof(float) && leftValueType == typeof(float)) {
				result = (float)rhs + (float)lhs;
			} else if (rightValueType == typeof(int) && leftValueType == typeof(int)) {
				result = (float)((int)rhs + (int)lhs);
			} else if (rightValueType == typeof(string) || leftValueType == typeof(string)) {
				result = ReturnValueConversions.PrettyStringRepresenation(lhs) + ReturnValueConversions.PrettyStringRepresenation(rhs);
			} else if (rightValueType == typeof(object[]) && leftValueType == typeof(object[])) {
				throw new Error("Primitive array concatenation is temporarily disabled.");
			} else if (rightValueType == typeof(SortedDictionary<KeyWrapper, object>) && leftValueType == typeof(SortedDictionary<KeyWrapper, object>)) {
				var lhsArray = lhs as SortedDictionary<KeyWrapper, object>;
				var rhsArray = rhs as SortedDictionary<KeyWrapper, object>;
				var newArray = new SortedDictionary<KeyWrapper, object>();
				for(int i = 0; i < lhsArray.Count; i++) {
					newArray.Add(new KeyWrapper((float)i), lhsArray[new KeyWrapper(i)]);
				}
				for(int i = 0; i < rhsArray.Count; i++) {
					newArray.Add(new KeyWrapper((float)(i + lhsArray.Count)), rhsArray[new KeyWrapper(i)]);
				}
				result = newArray;
			}
			else {
				throw new Error ("Can't add " + lhs + " to " + rhs);
			}		

            Trace(() => $"OPERATOR +: '{lhs}' + '{rhs}' -> '{result}'");
            return result;
		}

        private void ResolveVariableName()
        {
            string varName = CurrentNode.getTokenString();
            object value = m_currentScope.getValue(varName);
            Trace(() => $"LOAD_VAR '{varName}' -> '{value}' ({value.GetType().Name})");
            PushValue(value);
        }

		private void Not() {
			object a = PopValue();
			bool a_bool = ConvertToBool(a);
            bool result = !a_bool;
            Trace(() => $"OPERATOR !: !'{a}' -> {result}");
			PushValue(result);
		}
		
		private void ArrayLookup() 
		{
			object index = PopValue();
			object array = m_currentScope.getValue(CurrentNode.getTokenString());
			object val = null;

			if (array is Range) {
				if (index.GetType() == typeof(float)) {
					Range range = (Range)array;
					float i = range.step * (int)(float)index;
					float theNumber = range.start + i;
					float lowerBound = 0;
					float upperBound = 0;
					if (range.step > 0) {
						lowerBound = range.start;
						upperBound = range.end;
					} else {
						lowerBound = range.end;
						upperBound = range.start;
					}
					if (theNumber < lowerBound) {
						throw new Error ("Index " + index.ToString () + " is outside the range " + array.ToString ());
					} else if (theNumber > upperBound) {
						throw new Error ("Index " + index.ToString () + " is outside the range " + array.ToString ());
					}
					val = (float)theNumber;
				} else {
					throw new Error ("Can't look up " + index.ToString () + " in the range " + array.ToString ());
				}

			} else if (array.GetType() == typeof(SortedDictionary<KeyWrapper,object>)) {
				var a = array as SortedDictionary<KeyWrapper,object>;
				if (a.TryGetValue(new KeyWrapper(index), out val)) {
				} else {
					throw new Error ("Can't find the index '" + index + "' (" + ReturnValueConversions.PrettyObjectType(index.GetType ()) + ") in the array '" + CurrentNode.getTokenString () + "'", Error.ErrorType.RUNTIME, CurrentNode.getToken ().LineNr, CurrentNode.getToken ().LinePosition);
				}
			} else if (array.GetType() == typeof(object[])) {
				throw new Error("Illegal object[] array: " + ReturnValueConversions.PrettyStringRepresenation(array));
			} else if (array.GetType() == typeof(string)) {
				int i = 0;
				if(index.GetType() == typeof(float)) {
					i = (int)(float)index;
				}
				else if(index.GetType() == typeof(int)) {
					i = (int)index;
				} else {
					throw new Error("Must use nr when looking up index in string");
				}
				string s = (string)array;
				if (i >= 0 && i < s.Length) {
					val = s[i].ToString();
				} else {
					throw new Error ("The index '" + i + "' (" + index.GetType () + ") is outside the bounds of the string '" + CurrentNode.getTokenString () + "'", Error.ErrorType.RUNTIME, CurrentNode.getToken ().LineNr, CurrentNode.getToken ().LinePosition);
				}
			} else {
				throw new Error ("Can't convert " + array.ToString () + " to an array (for lookup)");
			}
			PushValue (val);
		}

		void PushValueFromToken ()
		{
			TokenWithValue t = CurrentNode.getToken() as TokenWithValue;
			if (t == null) {
				throw new Exception ("Can't convert current node to TokenWithValue: " + CurrentNode + ", it's of type " + CurrentNode.getTokenType());
			}
            var value = t.getValue();
            Trace(() => $"PUSH '{value}' ({value.GetType().Name})");
			PushValue(value);
		}

        private void VariableDeclaration()
        {
            ReturnValueType type = (CurrentNode as AST_VariableDeclaration).Type;
            string variableName = (CurrentNode as AST_VariableDeclaration).Name;
            object initValue = DefaultValue(type);
            m_currentScope.setValue(variableName, initValue);
        }

		object DefaultValue (ReturnValueType type)
		{
			if(type == ReturnValueType.STRING) {
				return "";
			}
			else if(type == ReturnValueType.BOOL) {
				return false;
			}
			else if(type == ReturnValueType.NUMBER) {
				return 0.0f;
			}
			else if(type == ReturnValueType.RANGE) {
				return new Range(0, 0, 0);
			}
			else if(type == ReturnValueType.ARRAY) {
				return new SortedDictionary<KeyWrapper, object>();
			}
			else if(type == ReturnValueType.VOID) {
				return VoidType.voidType;
			}
			else if(type == ReturnValueType.UNKNOWN_TYPE) {
				return UnknownType.unknownType;
			}
			else {
				throw new Error("No default value for " + type);
			}
		}
		
		private object ConvertToType(object valueToConvert, Type type) {
			var returnValueType = ReturnValueConversions.SystemTypeToReturnValueType(type);
			object newObject = ReturnValueConversions.ChangeTypeBasedOnReturnValueType(valueToConvert, returnValueType);
			return newObject;
		}

        private void AssignmentSignal()
        {
            string variableName = (CurrentNode as AST_Assignment).VariableName;
			object expressionValue = PopValue();
			Type type = m_currentScope.getValue(variableName).GetType();
			object convertedValue = ConvertToType(expressionValue, type);
			m_currentScope.setValue(variableName, convertedValue);
        }
		
		private void AssignmentToArrayElementSignal() {
			string variableName = (CurrentNode as AST_Assignment).VariableName;
			object valueToSet = PopValue();
			object index = PopValue();
			object rv = m_currentScope.getValue(variableName);

			if (rv.GetType () == typeof(SortedDictionary<KeyWrapper,object>)) {
				SortedDictionary<KeyWrapper, object> array = rv as SortedDictionary<KeyWrapper, object>;				
				if(array.ContainsKey(new KeyWrapper(index))) {
					array[new KeyWrapper(index)] = valueToSet;
				}
				else {
					array.Add(new KeyWrapper(index), valueToSet);
				}
			}
			else {
				var token = (CurrentNode as AST_Assignment).getToken();
				throw new Error ("Can't assign to the variable '" + variableName + "' since it's of the type " + ReturnValueConversions.PrettyObjectType(rv.GetType()), Error.ErrorType.RUNTIME, token.LineNr, token.LinePosition);
			}
		}

        private void ArrayEndSignal() 
		{
			AST_ArrayEndSignal arrayEndSignal = CurrentNode as AST_ArrayEndSignal;
			SortedDictionary<KeyWrapper, object> array = new SortedDictionary<KeyWrapper, object>();
			object[] values = new object[arrayEndSignal.ArraySize];
			for(int i = 0; i < arrayEndSignal.ArraySize; i++) {
				values[i] = PopValue();
			}
			for(int i = arrayEndSignal.ArraySize - 1; i >= 0; i--) {
				array.Add(new KeyWrapper((float)(arrayEndSignal.ArraySize - i - 1)), values[i]);
			}
			PushValue(array);
		}

        private void ReturnSignal()
        {
            while ( (m_currentScope.scopeType != Scope.ScopeType.FUNCTION_SCOPE) &&
			        (m_currentScope.scopeType != Scope.ScopeType.MAIN_SCOPE) )
            {
				PopCurrentScope();
			}
            m_currentMemorySpace.MoveToEnd();
        }
		
		static int loopBlockCounter = 0;
		private void LoopBlock() {
			AST_LoopBlockNode loopBlockNode = CurrentNode as AST_LoopBlockNode;
			#if DEBUG
			Debug.Assert(loopBlockNode != null);			
			#endif
			PushNewScope(loopBlockNode.getScope(), "LoopBlock_memorySpace" + loopBlockCounter++, loopBlockNode.getChild(0));
		}
		
		static int loopCounter = 0;
        private void Loop()
        {
			AST_LoopNode loopNode = CurrentNode as AST_LoopNode;
			#if DEBUG
			Debug.Assert(loopNode != null);
			#endif
			PushNewScope(loopNode.getScope(), "Loop_memorySpace_" + loopCounter++, loopNode.getChild(0));
        }

        private void BreakStatement()
        {
			while( (m_currentScope.scopeType != Scope.ScopeType.LOOP_SCOPE) &&
			       (m_currentScope.scopeType != Scope.ScopeType.MAIN_SCOPE) ) 
			{
				PopCurrentScope();
			}
			m_currentMemorySpace.MoveToEnd();
        }
		
		private void GotoBeginningOfLoop() 
		{
			PopCurrentScope();
			m_currentMemorySpace.Jump(-1);
		}
		
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
        
        public int stackSize
        {
            get { return m_memorySpaceStack.Count; }
        }

        public string DumpStack()
        {
            System.Text.StringBuilder b = new System.Text.StringBuilder();
			b.Append ("[");
            foreach (MemorySpace s in m_memorySpaceStack)
            {
				b.Append(" " + s.getName());
            }
			b.Append (" ]");
            return b.ToString();
        }
        
        public object PopValue()
        {
			if(m_valueStack.Count == 0) {
				throw new Error("Can't access value (have you forgotten to return a value from a function?)");
			}
#if PRINT_STACK
            	Console.WriteLine("Popping value " + m_valueStack.Peek());
#endif			
            object poppedValue = m_valueStack.Pop();
#if PRINT_STACK
			PrintMemoryStack();
            PrintValueStack();
#endif
            return poppedValue;
        }

        public void PushValue(object value)
        {
#if PRINT_STACK
            Console.WriteLine("Pushing value " + value);
#endif
            m_valueStack.Push(value);
#if PRINT_STACK
            PrintMemoryStack();
            PrintValueStack();
#endif
        }

        public bool ValueStackIsEmpty()
        {
            return m_valueStack.Count == 0;
        }

		// Profiling
		#if BUILT_IN_PROFILING
		public Dictionary<string, ProfileData> profileData {
			get {
				return m_profileData;
			}
		}
		public bool profilingOn = true;
		Dictionary<string, ProfileData> m_profileData = new Dictionary<string, ProfileData> ();
		#endif

		// Members
        AST m_ast;
        ExternalFunctionCreator m_externalFunctionCreator;
        ErrorHandler m_errorHandler;
        Scope m_globalScope;
        Scope m_currentScope;
        MemorySpace m_globalMemorySpace;
        MemorySpace m_currentMemorySpace;
        Stack<MemorySpace> m_memorySpaceStack = new Stack<MemorySpace>();
        Stack<object> m_valueStack = new Stack<object>();
		MemorySpaceNodeListCache m_memorySpaceNodeListCache = new MemorySpaceNodeListCache();
		int m_topLevelDepth = 0;
    }
}

public class ProfileData {
	public int calls;
	public float totalTime;
}