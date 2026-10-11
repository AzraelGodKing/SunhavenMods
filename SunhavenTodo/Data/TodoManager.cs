using System;
using System.Collections.Generic;
using System.Linq;

namespace SunhavenTodo.Data
{
    public class TodoManager
    {
        private TodoListData _todoData;
        private string _currentCharacter;
        private bool _isDirty;
        private readonly Dictionary<string, TodoItem> _todoById = new Dictionary<string, TodoItem>();
        private bool _queryCacheDirty = true;
        private readonly List<TodoItem> _activeTodosCache = new List<TodoItem>();
        private readonly List<TodoItem> _completedTodosCache = new List<TodoItem>();
        private readonly Dictionary<TodoCategory, List<TodoItem>> _todosByCategoryCache = new Dictionary<TodoCategory, List<TodoItem>>();

        public event Action OnTodosChanged;
        public event Action OnDataLoaded;

        public bool IsDirty => _isDirty;
        public string CurrentCharacter => _currentCharacter;

        public void LoadForCharacter(string characterName, TodoListData data)
        {
            _currentCharacter = characterName;
            _todoData = data ?? new TodoListData(characterName);
            _isDirty = false;
            RebuildTodoIndex();
            InvalidateQueryCaches();
            OnDataLoaded?.Invoke();
        }

        public TodoListData GetData()
        {
            return _todoData;
        }

        public void MarkClean()
        {
            _isDirty = false;
        }

        // CRUD Operations
        public void AddTodo(TodoItem item)
        {
            if (_todoData == null) return;

            _todoData.Items.Add(item);
            if (!string.IsNullOrEmpty(item.Id))
                _todoById[item.Id] = item;
            _todoData.LastUpdated = DateTime.Now;
            _isDirty = true;
            InvalidateQueryCaches();
            OnTodosChanged?.Invoke();
        }

        public TodoItem GetTodo(string id)
        {
            _todoById.TryGetValue(id, out var item);
            return item;
        }

        public void UpdateTodo(TodoItem item)
        {
            if (_todoData == null) return;

            var index = _todoData.Items.FindIndex(i => i.Id == item.Id);
            if (index >= 0)
            {
                _todoData.Items[index] = item;
                if (!string.IsNullOrEmpty(item.Id))
                    _todoById[item.Id] = item;
                _todoData.LastUpdated = DateTime.Now;
                _isDirty = true;
                InvalidateQueryCaches();
                OnTodosChanged?.Invoke();
            }
        }

        public void RemoveTodo(string itemId)
        {
            if (_todoData == null) return;

            var removed = _todoData.Items.RemoveAll(i => i.Id == itemId) > 0;
            if (removed)
            {
                _todoById.Remove(itemId);
                _todoData.LastUpdated = DateTime.Now;
                _isDirty = true;
                InvalidateQueryCaches();
                OnTodosChanged?.Invoke();
            }
        }

        public void ToggleComplete(string itemId)
        {
            if (_todoData == null) return;

            var item = GetTodoById(itemId);
            if (item != null)
            {
                item.IsCompleted = !item.IsCompleted;
                item.CompletedAt = item.IsCompleted ? DateTime.Now : (DateTime?)null;
                _todoData.LastUpdated = DateTime.Now;
                _isDirty = true;
                InvalidateQueryCaches();
                OnTodosChanged?.Invoke();
            }
        }

        /// <summary>
        /// Resets completed recurring todos so they appear active again.
        /// Call this on day/week/season transitions as appropriate.
        /// </summary>
        public void ResetRecurringTodos(RecurInterval interval)
        {
            if (_todoData == null) return;

            bool changed = false;
            foreach (var item in _todoData.Items)
            {
                if (item.IsCompleted && item.IsRecurring && item.RecurInterval == interval)
                {
                    item.IsCompleted = false;
                    item.CompletedAt = null;
                    changed = true;
                }
            }

            if (changed)
            {
                _todoData.LastUpdated = DateTime.Now;
                _isDirty = true;
                InvalidateQueryCaches();
                OnTodosChanged?.Invoke();
            }
        }

        public void ClearCompleted()
        {
            if (_todoData == null) return;

            // Only clear non-recurring completed todos
            var removed = _todoData.Items.RemoveAll(i => i.IsCompleted && !i.IsRecurring) > 0;
            if (removed)
            {
                RebuildTodoIndex();
                _todoData.LastUpdated = DateTime.Now;
                _isDirty = true;
                InvalidateQueryCaches();
                OnTodosChanged?.Invoke();
            }
        }

        // Query Operations
        public IEnumerable<TodoItem> GetAllTodos()
        {
            return _todoData?.Items ?? Enumerable.Empty<TodoItem>();
        }

        public IReadOnlyList<TodoItem> GetTodosByCategory(TodoCategory category)
        {
            EnsureQueryCaches();
            return _todosByCategoryCache.TryGetValue(category, out var todos) ? todos : Array.Empty<TodoItem>();
        }

        public IReadOnlyList<TodoItem> GetActiveTodos()
        {
            EnsureQueryCaches();
            return _activeTodosCache;
        }

        public IReadOnlyList<TodoItem> GetCompletedTodos()
        {
            EnsureQueryCaches();
            return _completedTodosCache;
        }

        public TodoItem GetTodoById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            return _todoById.TryGetValue(id, out var item) ? item : null;
        }

        // Statistics
        public (int total, int completed, int active) GetStats()
        {
            var todos = GetAllTodos().ToList();
            var completed = todos.Count(i => i.IsCompleted);
            return (todos.Count, completed, todos.Count - completed);
        }

        public Dictionary<TodoCategory, int> GetCountsByCategory()
        {
            var counts = new Dictionary<TodoCategory, int>();
            foreach (TodoCategory cat in Enum.GetValues(typeof(TodoCategory)))
            {
                counts[cat] = GetTodosByCategory(cat).Count(i => !i.IsCompleted);
            }
            return counts;
        }

        public float GetCompletionPercent()
        {
            var (total, completed, _) = GetStats();
            return total == 0 ? 0 : (completed / (float)total) * 100f;
        }

        private void RebuildTodoIndex()
        {
            _todoById.Clear();
            if (_todoData?.Items == null)
                return;

            foreach (var todo in _todoData.Items)
            {
                if (todo != null && !string.IsNullOrEmpty(todo.Id))
                    _todoById[todo.Id] = todo;
            }
        }

        private void InvalidateQueryCaches()
        {
            _queryCacheDirty = true;
        }

        private void EnsureQueryCaches()
        {
            if (!_queryCacheDirty)
                return;

            _activeTodosCache.Clear();
            _completedTodosCache.Clear();
            _todosByCategoryCache.Clear();

            if (_todoData?.Items != null)
            {
                foreach (var todo in _todoData.Items)
                {
                    if (todo == null)
                        continue;

                    if (todo.IsCompleted)
                        _completedTodosCache.Add(todo);
                    else
                        _activeTodosCache.Add(todo);

                    if (!_todosByCategoryCache.TryGetValue(todo.Category, out var categoryList))
                    {
                        categoryList = new List<TodoItem>();
                        _todosByCategoryCache[todo.Category] = categoryList;
                    }
                    categoryList.Add(todo);
                }
            }

            _queryCacheDirty = false;
        }
    }
}
