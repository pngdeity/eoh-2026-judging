using System.Collections.Generic;
using System.Linq;

using ContestJudging.Core.Entities;

namespace ContestJudging.Services.Validation
{
    public class GraphValidationService : IValidationService
    {
        private class UnionFind
        {
            private readonly Dictionary<string, string> _parent = new();

            public UnionFind(IEnumerable<string> elements)
            {
                foreach (var el in elements)
                {
                    _parent[el] = el;
                }
            }

            public string Find(string i)
            {
                if (_parent[i] == i)
                {
                    return i;
                }
                _parent[i] = Find(_parent[i]);
                return _parent[i];
            }

            public void Union(string i, string j)
            {
                string rootI = Find(i);
                string rootJ = Find(j);
                if (rootI != rootJ)
                {
                    _parent[rootI] = rootJ;
                }
            }
        }

        public bool IsTotalOrder(IEnumerable<Relation> relations, IEnumerable<string> allEntryIds)
        {
            var allEntryIdsList = allEntryIds.ToList();
            var uf = new UnionFind(allEntryIdsList);
            var relationsList = relations.ToList();

            foreach (var rel in relationsList)
            {
                if (rel.Operator == Operator.EqualTo)
                {
                    uf.Union(rel.EntryA.Id, rel.EntryB.Id);
                }
            }

            var adjList = new Dictionary<string, HashSet<string>>();
            var inDegree = new Dictionary<string, int>();

            foreach (var entryId in allEntryIdsList)
            {
                string root = uf.Find(entryId);
                if (!inDegree.ContainsKey(root))
                {
                    inDegree[root] = 0;
                }
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

                if (u == v) return false;

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

            var queue = new Queue<string>(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
            int processedNodes = 0;

            while (queue.Count > 0)
            {
                if (queue.Count > 1) return false;
                string u = queue.Dequeue();
                processedNodes++;

                if (adjList.ContainsKey(u))
                {
                    foreach (var v in adjList[u])
                    {
                        inDegree[v]--;
                        if (inDegree[v] == 0)
                        {
                            queue.Enqueue(v);
                        }
                    }
                }
            }

            return processedNodes == inDegree.Count;
        }

        public bool IsValidOrder(IEnumerable<Relation> relations, IEnumerable<string> allEntryIds)
        {
            var allEntryIdsList = allEntryIds.ToList();
            var uf = new UnionFind(allEntryIdsList);
            var relationsList = relations.ToList();

            foreach (var rel in relationsList)
            {
                if (rel.Operator == Operator.EqualTo)
                {
                    uf.Union(rel.EntryA.Id, rel.EntryB.Id);
                }
            }

            var adjList = new Dictionary<string, HashSet<string>>();
            var inDegree = new Dictionary<string, int>();

            foreach (var entryId in allEntryIdsList)
            {
                string root = uf.Find(entryId);
                if (!inDegree.ContainsKey(root))
                {
                    inDegree[root] = 0;
                }
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

                if (u == v) return false;

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

            var queue = new Queue<string>(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
            int processedNodes = 0;

            while (queue.Count > 0)
            {
                string u = queue.Dequeue();
                processedNodes++;

                if (adjList.ContainsKey(u))
                {
                    foreach (var v in adjList[u])
                    {
                        inDegree[v]--;
                        if (inDegree[v] == 0)
                        {
                            queue.Enqueue(v);
                        }
                    }
                }
            }

            return processedNodes == inDegree.Count;
        }

        public List<HashSet<string>> GetSortedTiers(IEnumerable<Relation> relations, IEnumerable<string> allEntryIds)
        {
            var allEntryIdsList = allEntryIds.ToList();
            var uf = new UnionFind(allEntryIdsList);
            var relationsList = relations.ToList();

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

                if (u == v) continue;

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

            var queue = new Queue<string>(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
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

                    if (adjList.ContainsKey(u))
                    {
                        foreach (var v in adjList[u])
                        {
                            inDegree[v]--;
                            if (inDegree[v] == 0)
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
    }
}
