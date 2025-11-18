// In ProgrammingLanguageNr1 folder

using System.Collections.Generic;

namespace ProgrammingLanguageNr1
{
    /// <summary>
    /// A container for the result of flattening an AST. It holds both the
    /// linear list of nodes and a map of any labels found.
    /// </summary>
    public class CacheResult
    {
        public readonly IReadOnlyList<AST> Nodes;
        public readonly IReadOnlyDictionary<string, int> LabelMap;

        public CacheResult(List<AST> nodes, Dictionary<string, int> labelMap)
        {
            Nodes = nodes;
            LabelMap = labelMap;
        }
    }

    /// <summary>
    /// Flattens an AST into a linear list of executable nodes for the interpreter's MemorySpace.
    /// It caches the result so that the flattening process (which can be expensive) only happens
    //  once per AST section (e.g., once per function body). It also builds a map of all GOTO labels.
    /// </summary>
    public class MemorySpaceNodeListCache
    {
        private readonly Dictionary<AST, CacheResult> m_cache = new Dictionary<AST, CacheResult>();

        /// <summary>
        /// Gets a flattened list of nodes and a label map for a given AST.
        /// If the AST has been processed before, a cached result is returned instantly.
        /// </summary>
        /// <param name="headNode">The root of the AST or AST sub-tree to process.</param>
        /// <returns>A CacheResult containing the linear node list and label map.</returns>
        public CacheResult GetNodeList(AST headNode)
        {
            if (m_cache.TryGetValue(headNode, out var cachedResult))
            {
                return cachedResult;
            }

            var nodes = new List<AST>();
            var labelMap = new Dictionary<string, int>();
            
            AddChildrenToListRecursive(headNode, nodes, labelMap);

            var newResult = new CacheResult(nodes, labelMap);
            m_cache[headNode] = newResult;
            return newResult;
        }

        /// <summary>
        /// Recursively traverses the AST using a post-order traversal to build the linear instruction list.
        /// This ensures that operands of an expression are added to the list before their operator.
        /// </summary>
        private void AddChildrenToListRecursive(AST node, List<AST> list, Dictionary<string, int> labelMap)
        {
            if (node == null) return;

            // First, recurse into all children. This is the essence of post-order traversal.
            if (node.getChildren() != null)
            {
                foreach (AST child in node.getChildren())
                {
                    AddChildrenToListRecursive(child, list, labelMap);
                }
            }

            // After all children have been processed and added to the list, add the parent node itself.
            list.Add(node);

            // If this node is a LABEL, record its string name and its current position in the flattened list.
            // The position is the index of the item we just added.
            if (node.getTokenType() == Token.TokenType.LABEL)
            {
                labelMap[node.getTokenString()] = list.Count - 1;
            }
        }
		
        /// <summary>
        /// Clears the cache. This should be called when the interpreter is reset.
        /// </summary>
        public void clear()
        {
            m_cache.Clear();
        }
    }
}