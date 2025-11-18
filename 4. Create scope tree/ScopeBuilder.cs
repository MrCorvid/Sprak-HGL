//#define WRITE_DEBUG_INFO

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ProgrammingLanguageNr1
{
	public class ScopeBuilder
	{
		public ScopeBuilder (AST ast, ErrorHandler errorHandler)
		{
			Debug.Assert(ast != null);
			Debug.Assert(errorHandler != null);

            m_errorHandler = errorHandler;
			m_ast = ast;
		}
		
		public void process() {
			m_globalScope = new Scope(Scope.ScopeType.MAIN_SCOPE, "global scope");
			m_currentScope = m_globalScope;

            #if WRITE_DEBUG_INFO
            Console.WriteLine("Evaluate scope declarations:");
            #endif

			evaluateScopeDeclarations(m_ast);

            #if WRITE_DEBUG_INFO
            Console.WriteLine("\nEvaluate references:");
            #endif

			evaluateReferences(m_ast);
		}
		
		private void evaluateScopeDeclarations(AST tree) {
			Debug.Assert(tree != null);
			
			// Note: The new GOTO and LABEL tokens do not declare scopes,
            // so no changes are needed here. The logic correctly falls through
            // to the final 'else if' and traverses into children where appropriate.
			
			if (tree.getTokenType() == Token.TokenType.FUNC_DECLARATION) 
			{
                evaluateFunctionScope(tree);
			}
			else if (tree.getTokenType() == Token.TokenType.IF) {
                evaluateIfScope(tree);
			}
			else if (tree.getTokenType() == Token.TokenType.LOOP) {
                evaluateLoopScope(tree);
			}
			else if (tree.getTokenType() == Token.TokenType.LOOP_BLOCK) {
               	evaluateLoopBlockScope(tree);
			}
			else if (tree.getChildren() != null) 
			{
				evaluateScopeDeclarationsInAllChildren(tree);
			}
		}

        private void evaluateScopeDeclarationsInAllChildren(AST tree)
        {
            foreach (AST subtree in tree.getChildren())
            {
                evaluateScopeDeclarations(subtree);
            }
        }

        private void evaluateFunctionScope(AST tree)
        {
            ReturnValueType returnType = ExternalFunctionCreator.GetReturnTypeFromString(tree.getChild(0).getTokenString());
            string functionName = tree.getChild(1).getTokenString();
            Symbol functionScope = new FunctionSymbol(m_currentScope, functionName, returnType, tree);
            m_globalScope.define(functionScope);
            
			m_currentScope = (Scope)functionScope;
            AST_FunctionDefinitionNode functionCallNode = (AST_FunctionDefinitionNode)(tree);
            functionCallNode.setScope((Scope)functionScope);

            evaluateScopeDeclarations(tree.getChild(3));
            m_currentScope = m_currentScope.getEnclosingScope();
        }

        private void evaluateIfScope(AST tree)
        {
            Scope subscope = new Scope(Scope.ScopeType.IF_SCOPE,"<IF-SUBSCOPE>", m_currentScope);
            m_currentScope = subscope;
            AST_IfNode ifNode = (tree as AST_IfNode);
            Debug.Assert(ifNode != null);
            ifNode.setScope(subscope);
            
            evaluateScopeDeclarationsInAllChildren(tree.getChild(0));
            AST trueNode = ifNode.getChild(1);
            AST falseNode = (ifNode.getChildren().Count == 3) ? ifNode.getChild(2) : null;

			evaluateScopeDeclarationsInAllChildren(trueNode);
            if (falseNode != null)
            {
                evaluateScopeDeclarationsInAllChildren(falseNode);
            }
            m_currentScope = m_currentScope.getEnclosingScope();
        }

		static int loopSubscopes = 0;

		private void evaluateLoopScope(AST tree)
        {
			Scope subscope = new Scope(Scope.ScopeType.LOOP_SCOPE, "<LOOP-SUBSCOPE " + (loopSubscopes++) + ">", m_currentScope);
			m_currentScope = subscope;
            AST_LoopNode loopNode = (tree as AST_LoopNode);
            Debug.Assert(loopNode != null);
            evaluateScopeDeclarationsInAllChildren(loopNode);
			loopNode.setScope(m_currentScope);
            m_currentScope = m_currentScope.getEnclosingScope();
        }

		static int loopBlockSubscopes = 0;

		private void evaluateLoopBlockScope(AST tree)
        {
			Scope subscope = new Scope(Scope.ScopeType.LOOP_BLOCK_SCOPE, "<LOOP-BLOCK-SUBSCOPE " + (loopBlockSubscopes++) + ">", m_currentScope);
			m_currentScope = subscope;
            AST_LoopBlockNode loopBlockNode = (tree as AST_LoopBlockNode);
            Debug.Assert(loopBlockNode != null);
            evaluateScopeDeclarationsInAllChildren(loopBlockNode);
			loopBlockNode.setScope(m_currentScope);
            m_currentScope = m_currentScope.getEnclosingScope();
        }

		private void evaluateReferences(AST tree) {
			Debug.Assert(tree != null);
			
			// START OF MODIFICATION: This is the critical change to fix the crash.
            if (tree.getTokenType() == Token.TokenType.LABEL)
            {
                // A label is a declaration point, not a variable reference. It has no children
                // that need to be resolved, so we do nothing and stop recursion for this branch.
            }
            else if (tree.getTokenType() == Token.TokenType.GOTO)
            {
                // A GOTO is a control flow instruction. Its child is the name of a label, NOT a variable.
                // We must NOT traverse into its children, otherwise the builder will try to resolve
                // the label name as a variable and fail. We stop recursion for this branch here.
            }
            // END OF MODIFICATION
			else if (tree.getTokenType() == Token.TokenType.VAR_DECLARATION) 
			{
                evaluateReferencesForVAR_DECLARATION(tree);
			}
			else if (tree.getTokenType() == Token.TokenType.ASSIGNMENT) 
			{
                evaluateReferencesForASSIGNMENT(tree);
			}
			else if (tree.getTokenType() == Token.TokenType.ASSIGNMENT_TO_ARRAY) 
			{
				evaluateReferencesForASSIGNMENT_TO_ARRAY(tree);
			}
			else if (tree.getTokenType() == Token.TokenType.ARRAY_LOOKUP) 
			{
				evaluateReferencesForARRAY_LOOKUP(tree);
			}
			else if (tree.getTokenType() == Token.TokenType.FUNC_DECLARATION) 
			{
                evaluateReferencesForFUNC_DECLARATION(tree);
			}
			else if (tree.getTokenType() == Token.TokenType.FUNCTION_CALL) 
			{
                evaluateReferencesForFUNCTION_CALL(tree);
			}
			else if (tree.getTokenType() == Token.TokenType.IF) 
			{
                evaluateReferencesForIF(tree);
			}
			else if (tree.getTokenType() == Token.TokenType.NAME) 
			{
                evaluateReferencesForNAME(tree);
			}
			else if (tree.getTokenType() == Token.TokenType.LOOP_BLOCK) 
			{
                evaluateReferencesForLOOP_BLOCK(tree);
			}
			else if (tree.getTokenType() == Token.TokenType.LOOP) 
			{
                evaluateReferencesForLOOP(tree);
			}
			else 
			{
				evaluateReferencesInAllChildren(tree);
			}
		}
		
        private void evaluateReferencesInAllChildren(AST tree)
        {
            if (tree.getChildren() != null)
            {
                // Go through all other subtrees
                foreach (AST subtree in tree.getChildren())
                {
                    evaluateReferences(subtree);
                }
            }
        }
		
		private void evaluateReferencesForASSIGNMENT(AST tree)
		{
			AST_Assignment assignment = tree as AST_Assignment;
			Symbol variableNameSymbol = m_currentScope.resolve(assignment.VariableName);
			if(variableNameSymbol == null) {
				m_errorHandler.errorOccured("Can't assign to undefined variable " + assignment.VariableName,
				                Error.ErrorType.SYNTAX, tree.getToken().LineNr, tree.getToken().LinePosition);
			}
			evaluateReferencesInAllChildren(tree);
		}
		
		private void evaluateReferencesForASSIGNMENT_TO_ARRAY(AST tree)
		{
			AST_Assignment assignment = tree as AST_Assignment;
			Symbol variableNameSymbol = m_currentScope.resolve(assignment.VariableName);
			if(variableNameSymbol == null) {
				m_errorHandler.errorOccured("Can't assign to undefined array " + assignment.VariableName,
				                Error.ErrorType.SYNTAX, tree.getToken().LineNr, tree.getToken().LinePosition);
			}
			evaluateReferencesInAllChildren(tree);
		}

		private void evaluateReferencesForARRAY_LOOKUP(AST tree)
		{
			AST lookup = tree;
			Symbol variableNameSymbol = m_currentScope.resolve(lookup.getTokenString());
			if(variableNameSymbol == null) {
				m_errorHandler.errorOccured("Can't lookup in undefined array " + lookup.getTokenString(),
				                            Error.ErrorType.SYNTAX, lookup.getToken().LineNr, lookup.getToken().LinePosition);
			}
			evaluateReferencesInAllChildren(tree);
		}

        private void evaluateReferencesForVAR_DECLARATION(AST tree)
        {
            AST_VariableDeclaration varDeclaration = tree as AST_VariableDeclaration;
            if (m_currentScope.isDefined(varDeclaration.Name))
            {
                m_errorHandler.errorOccured(
                    new Error("There is already a variable called '" + varDeclaration.Name + "'",
                    Error.ErrorType.LOGIC, tree.getToken().LineNr, tree.getToken().LinePosition));
            }
            else
            {
                m_currentScope.define(new VariableSymbol(varDeclaration.Name, varDeclaration.Type));
            }
        }

        private void evaluateReferencesForFUNCTION_CALL(AST tree)
        {
            string functionName = tree.getTokenString();
			var sym = m_currentScope.resolve (functionName);
			FunctionSymbol function = sym as FunctionSymbol;
            if (function == null)
            {
                m_errorHandler.errorOccured("Can't find function with name " + functionName, 
				                            Error.ErrorType.SCOPE, tree.getToken().LineNr, tree.getToken().LinePosition);
            }
            else
            {
                evaluateReferencesInAllChildren(tree);
                AST_FunctionDefinitionNode functionDefinitionTree = (AST_FunctionDefinitionNode)(function.getFunctionDefinitionNode());
                AST_FunctionCall functionCallAst = tree as AST_FunctionCall;
                Debug.Assert(functionCallAst != null);
				functionCallAst.FunctionDefinitionRef = functionDefinitionTree;
				
                List<AST> calleeParameterList = functionDefinitionTree.getChild(2).getChildren();
                List<AST> arguments = tree.getChild(0).getChildren();
                if (arguments.Count != calleeParameterList.Count)
                {
                    m_errorHandler.errorOccured(
						"Wrong nr of arguments to  '" + functionDefinitionTree.getChild(1).getTokenString() + "' , expected " + calleeParameterList.Count + " but got " + arguments.Count,
                         Error.ErrorType.SYNTAX, tree.getToken().LineNr, tree.getToken().LinePosition);
                }
            }
        }

        private void evaluateReferencesForIF(AST tree)
        {
            AST_IfNode ifNode = (AST_IfNode)(tree);
            m_currentScope = (Scope)ifNode.getScope(); // push IF-subscope
            evaluateReferencesInAllChildren(tree);
            m_currentScope = m_currentScope.getEnclosingScope(); // pop scope
        }

        private void evaluateReferencesForNAME(AST tree)
        {
            Symbol symbol = m_currentScope.resolve(tree.getTokenString());
			if(symbol == null) {
				m_errorHandler.errorOccured(
					new  Error("Can't find variable or function '" + tree.getTokenString() + "' (forgot quotes?)", 
				                                       Error.ErrorType.SYNTAX, tree.getToken().LineNr, tree.getToken().LinePosition));
			}
			else if (symbol is FunctionSymbol) {
				m_errorHandler.errorOccured(
				                            new  Error("'" + tree.getTokenString() + "' is a function and must be called with ()", 
				                                       Error.ErrorType.SYNTAX, tree.getToken().LineNr, tree.getToken().LinePosition));
			}
            evaluateReferencesInAllChildren(tree);
        }

        private void evaluateReferencesForFUNC_DECLARATION(AST tree)
        {
            string functionName = tree.getChild(1).getTokenString();
            m_currentScope = (Scope)m_currentScope.resolve(functionName);
            evaluateReferencesInAllChildren(tree.getChild(2));
            evaluateReferencesInAllChildren(tree.getChild(3));
            m_currentScope = m_currentScope.getEnclosingScope();
        }   
		
		private void evaluateReferencesForLOOP_BLOCK(AST tree)
        {
			AST_LoopBlockNode loopBlockNode = tree as AST_LoopBlockNode;
            m_currentScope = loopBlockNode.getScope();
            evaluateReferencesInAllChildren(tree);
            m_currentScope = m_currentScope.getEnclosingScope();
        }  
		
		private void evaluateReferencesForLOOP(AST tree)
        {
			AST_LoopNode loopBlockNode = tree as AST_LoopNode;
            m_currentScope = loopBlockNode.getScope();
            evaluateReferencesInAllChildren(tree);
            m_currentScope = m_currentScope.getEnclosingScope();
        }
		
		public Scope getGlobalScope() {
			Debug.Assert(m_globalScope != null, "The global scope is null, this probably means that you haven't called process() on ScopeBuilder");
			return m_globalScope;
		}
		
		AST m_ast;
		Scope m_globalScope;
		Scope m_currentScope;
		ErrorHandler m_errorHandler;
	}
}