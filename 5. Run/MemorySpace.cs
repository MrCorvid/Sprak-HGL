// In ProgrammingLanguageNr1 folder

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProgrammingLanguageNr1
{
	public class MemorySpace
	{
		public static int nrOfMemorySpacesInMemory = 0;

		public MemorySpace (string name, AST root, Scope scope, MemorySpaceNodeListCache cache)
		{
            Debug.Assert(name != null);
            Debug.Assert(root != null);
            Debug.Assert(scope != null);
			Debug.Assert(cache != null);

			m_name = name;
            m_scope = scope;
			
            // Use the cache to get the flattened list of nodes and the map of labels.
            // This is the central change from the old implementation.
			var cacheResult = cache.GetNodeList(root);
            m_nodes = cacheResult.Nodes;
            m_labelMap = cacheResult.LabelMap;
            
            m_currentNode = -1; // Start before the first instruction.

			nrOfMemorySpacesInMemory++;
		}

		~MemorySpace() {
			nrOfMemorySpacesInMemory--;
		}

        // The old private methods 'addToList' and 'addChildren' have been removed,
        // as this logic is now correctly handled by MemorySpaceNodeListCache.
		
		public void setValue(string name, object val) {
            Debug.Assert(name != null);
            Debug.Assert(val != null);

			if(m_valuesForStrings.ContainsKey(name)) {
				m_valuesForStrings[name] = val;
			} else {
				m_valuesForStrings.Add(name, val);
			}
		}

        public bool hasValue(string name)
        {
            return m_valuesForStrings.ContainsKey(name);
        }
		
		public object getValue(string name) {
            Debug.Assert(name != null);

			if(!m_valuesForStrings.ContainsKey(name)) {
				throw new Error("Can't find variable with name '" + name + "' (forgot quotes?)");
			}
			
			return m_valuesForStrings[name];
		}

        public void PrintValues()
        {
            foreach (string name in m_valuesForStrings.Keys)
            {
                Console.WriteLine("\t\t" + name + " = " + m_valuesForStrings[name].ToString());
            }
        }
		
		public string getName() {
			return m_name;
		}

        public AST CurrentNode
        {
            get
            {
                if (m_currentNode >= 0 && m_currentNode < m_nodes.Count)
                {
                    return m_nodes[m_currentNode];
                }
                // This might happen if the program counter is manipulated unexpectedly.
                // Returning a dummy EOF node can prevent crashes in some edge cases.
                return new AST(new Token(Token.TokenType.EOF, "<OUT_OF_BOUNDS>"));
            }
        }

        public bool Next()
        {
            m_currentNode++;
            return m_currentNode < m_nodes.Count;
        }
		
		public void MoveToStart()
        {
            m_currentNode = -1;
        }
		
        public void MoveToEnd()
        {
            m_currentNode = m_nodes.Count;
        }
		
		public void Jump(int steps)
        {
            m_currentNode += steps;
        }

        /// <summary>
        /// Jumps the program counter to the specified label name.
        /// </summary>
        public void JumpToLabel(string label)
        {
            if (m_labelMap.TryGetValue(label, out int index))
            {
                // Set the program counter to the instruction *before* the label.
                // The interpreter's main loop will call Next() immediately after this,
                // which increments the counter to the correct target instruction.
                m_currentNode = index - 1;
            }
            else
            {
                throw new Error($"Runtime error: Can't find label '{label}' in the current execution scope '{m_name}'.");
            }
        }

        public Scope Scope
        {
            get { return m_scope; }
        }
				
		public void Delete() {
			m_name = "";
			m_valuesForStrings = null;
			m_nodes = null;
            m_labelMap = null;
			m_currentNode = -1;
			m_scope = null;
		}

		string m_name;
        Dictionary<string, object> m_valuesForStrings = new Dictionary<string, object>();
        IReadOnlyList<AST> m_nodes;
        IReadOnlyDictionary<string, int> m_labelMap; // Stores locations of labels for GOTO.
        int m_currentNode;
        Scope m_scope;
	}
}