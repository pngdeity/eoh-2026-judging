using System;
using System.Collections.Generic;
using System.Linq;

using ContestJudging.Core.Entities;

namespace ContestJudging.Services.Validation
{
    public sealed class GraphValidationService : IValidationService
    {
        private sealed class UnionFind
        {
            private readonly Dictionary<string, string> _parent = new();
            private readonly Dictionary<string, int> _rank = new();

            public UnionFind(IEnumerable<string> elements)
            {
                foreach (var el in elements)
                {
                    _parent[el] = el;
                    _rank[el] = 0;
                }
            }

            public string Find(string i)
            {
                if (_parent[i] != i)
                    _parent[i] = Find(_parent[i]);
                return _parent[i];
            }

            public void Union(string i, string j)
            {
                string rootI = Find(i);
                string rootJ = Find(j);
                if (rootI == rootJ) return;

                if (_rank[rootI] < _rank[rootJ])
                    _parent[rootI] = rootJ;
                else if (_rank[rootI] > _rank[rootJ])
                    _parent[rootJ] = rootI;
                else
                {
                    _parent[rootJ] = rootI;
                    _rank[rootI]++;
                }
            }
        }

        private static (UnionFind uf, Dictionary<string, HashSet<string>> adjList,
                 Dictionary<string, int> inDegree, Dictionary<string, HashSet<string>> rootToMembers)
            BuildTopologicalGraph(IEnumerable<Relation> relations, IEnumerable<string> allEntryIds)
        {
            var allEntryIdsList = allEntryIds.ToList();
            var relationsList = relations.ToList();
            var uf = new UnionFind(allEntryIdsList);

            foreach (var rel in relationsList)
            {
                if (rel.Operator == Operator.EqualTo)
                {
                    uf.Union(rel.EntryA.Id, rel.EntryB.Id);
                }
            }

            var rootToMembers = new Dictionary<string, HashSet<string>>();
            var adjList = new Dictionary<string, HashSet<string>>();
            var inDegree = new Dictionary<string, int>();

            foreach (var entryId in allEntryIdsList)
            {
                string root = uf.Find(entryId);
                if (!rootToMembers.ContainsKey(root))
                {
                    rootToMembers[root] = new HashSet<string>();
                    inDegree[root] = 0;
                }
                rootToMembers[root].Add(entryId);
            }

            foreach (var rel in relationsList)
            {
                string rootA = uf.Find(rel.EntryA.Id);
                string rootB = uf.Find(rel.EntryB.Id);

                string u, v;
                if (rel.Operator == Operator.GreaterThan)
                {
                    u = rootA;
                    v = rootB;
                }
                else if (rel.Operator == Operator.LessThan)
                {
                    u = rootB;
                    v = rootA;
                }
                else
                {
                    continue;
                }

                if (u == v)
                    throw new InvalidOperationException("Self-referencing relation: an entry cannot be compared to itself with a non-equality operator.");

                if (!adjList.ContainsKey(u))
                {
                    adjList[u] = new HashSet<string>();
                }

                if (!adjList[u].Contains(v))
                {
                    adjList[u].Add(v);
                    inDegree[v]++;
                }
            }

            return (uf, adjList, inDegree, rootToMembers);
        }

        private static bool TryTopologicalSort(
            Dictionary<string, HashSet<string>> adjList,
            Dictionary<string, int> inDegree,
            out List<string> sorted,
            bool checkUnique = false)
        {
            sorted = new List<string>();
            var workingInDeg = new Dictionary<string, int>(inDegree);
            var queue = new Queue<string>(workingInDeg.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));

            while (queue.Count > 0)
            {
                if (checkUnique && queue.Count > 1) return false;

                string u = queue.Dequeue();
                sorted.Add(u);

                if (adjList.ContainsKey(u))
                {
                    foreach (var v in adjList[u])
                    {
                        workingInDeg[v]--;
                        if (workingInDeg[v] == 0)
                        {
                            queue.Enqueue(v);
                        }
                    }
                }
            }

            return sorted.Count == inDegree.Count;
        }

        public bool IsTotalOrder(IEnumerable<Relation> relations, IEnumerable<string> allEntryIds)
        {
            try
            {
                var (_, adj, inDeg, _) = BuildTopologicalGraph(relations, allEntryIds);
                return TryTopologicalSort(adj, inDeg, out _, checkUnique: true);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public bool IsValidOrder(IEnumerable<Relation> relations, IEnumerable<string> allEntryIds)
        {
            try
            {
                var (_, adj, inDeg, _) = BuildTopologicalGraph(relations, allEntryIds);
                return TryTopologicalSort(adj, inDeg, out _);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public List<HashSet<string>> GetSortedTiers(IEnumerable<Relation> relations, IEnumerable<string> allEntryIds)
        {
            var (_, adj, inDeg, rootToMembers) = BuildTopologicalGraph(relations, allEntryIds);

            var workingInDeg = new Dictionary<string, int>(inDeg);
            var queue = new Queue<string>(workingInDeg.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
            var sortedTiers = new List<HashSet<string>>();

            while (queue.Count > 0)
            {
                int currentBatchSize = queue.Count;
                var currentTier = new HashSet<string>();
                for (int i = 0; i < currentBatchSize; i++)
                {
                    string u = queue.Dequeue();
                    foreach (var member in rootToMembers[u])
                    {
                        currentTier.Add(member);
                    }

                    if (adj.ContainsKey(u))
                    {
                        foreach (var v in adj[u])
                        {
                            workingInDeg[v]--;
                            if (workingInDeg[v] == 0)
                            {
                                queue.Enqueue(v);
                            }
                        }
                    }
                }
                sortedTiers.Add(currentTier);
            }

            sortedTiers.Reverse();
            return sortedTiers;
        }

        public ValidationResult ValidatePartitionedGraph(IEnumerable<Relation> globalRelations, IEnumerable<string> allEntryIds)
        {
            var relationsList = globalRelations.ToList();
            var allEntryIdsList = allEntryIds.ToList();

            // 1. Cycle Detection (and Transitive Reduction / Tie Handling)
            if (!IsValidOrder(relationsList, allEntryIdsList))
            {
                return new ValidationResult(false, "The judging graph contains cycles.", 0);
            }

            // 2. Connectivity Check (Using Undirected Union-Find)
            var connectivityUf = new UnionFind(allEntryIdsList);
            foreach (var rel in relationsList)
            {
                connectivityUf.Union(rel.EntryA.Id, rel.EntryB.Id);
            }

            var uniqueRoots = new HashSet<string>();
            foreach (var entryId in allEntryIdsList)
            {
                uniqueRoots.Add(connectivityUf.Find(entryId));
            }

            int componentCount = uniqueRoots.Count;
            if (componentCount > 1)
            {
                return new ValidationResult(false, "The graph is not fully connected. Bridge nodes failed to overlap correctly.", componentCount);
            }

            return new ValidationResult(true, string.Empty, 1);
        }
    }
}
