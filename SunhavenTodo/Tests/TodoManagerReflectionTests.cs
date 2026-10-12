using System;
using NUnit.Framework;
using SunhavenMods.Shared;
using SunhavenTodo.Data;

namespace SunhavenTodo.Tests
{
    /// <summary>
    /// Binds the suite's shared Todo client to the real TodoManager. A rename or signature change in
    /// Todo that would silently disable Birthday Reminder, Senpai's Chest, Gifting Assistant, or
    /// Crop Optimizer fails here instead (AZR-255).
    /// </summary>
    [TestFixture]
    public class TodoManagerReflectionTests
    {
        private TodoManager _manager;
        private TodoManagerReflection _todo;

        [SetUp]
        public void SetUp()
        {
            _manager = new TodoManager();
            _manager.LoadForCharacter("Ayla", new TodoListData("Ayla"));
            _todo = TodoManagerReflection.TryBind(_manager, out string error);
            Assert.That(error, Is.Null);
            Assert.That(_todo, Is.Not.Null);
        }

        [Test]
        public void TryBind_RealManager_ExposesCharacterAndData()
        {
            Assert.That(_todo.CurrentCharacter, Is.EqualTo("Ayla"));
            Assert.That(_todo.HasData, Is.True);
        }

        [Test]
        public void HasData_FalseBeforeACharacterLoads()
        {
            var empty = TodoManagerReflection.TryBind(new TodoManager(), out _);
            Assert.That(empty.HasData, Is.False);
        }

        [Test]
        public void TryBind_RejectsNullAndForeignObjects()
        {
            Assert.That(TodoManagerReflection.TryBind(null, out string nullError), Is.Null);
            Assert.That(nullError, Is.Not.Null);
            Assert.That(TodoManagerReflection.TryBind(new object(), out string foreignError), Is.Null);
            Assert.That(foreignError, Does.Contain("types not found"));
        }

        [Test]
        public void TypeNameConstants_MatchTodoTypes()
        {
            Assert.That(typeof(TodoItem).FullName, Is.EqualTo(TodoManagerReflection.TodoItemTypeName));
            Assert.That(typeof(TodoPriority).FullName, Is.EqualTo(TodoManagerReflection.TodoPriorityTypeName));
            Assert.That(typeof(TodoCategory).FullName, Is.EqualTo(TodoManagerReflection.TodoCategoryTypeName));
        }

        [Test]
        public void AddTodo_SetsEveryDraftField()
        {
            string id = _todo.AddTodo(new TodoDraft
            {
                Title = "Donate Gold Ore -> Hall of Gems",
                Description = "Found in a chest.",
                Priority = "High",
                Category = "Collection",
                IconItemId = 1234,
                MuseumDestination = "Hall of Gems"
            });

            TodoItem item = _manager.GetTodoById(id);
            Assert.That(item, Is.Not.Null);
            Assert.That(item.Title, Is.EqualTo("Donate Gold Ore -> Hall of Gems"));
            Assert.That(item.Description, Is.EqualTo("Found in a chest."));
            Assert.That(item.Priority, Is.EqualTo(TodoPriority.High));
            Assert.That(item.Category, Is.EqualTo(TodoCategory.Collection));
            Assert.That(item.IconItemId, Is.EqualTo(1234));
            Assert.That(item.MuseumDestination, Is.EqualTo("Hall of Gems"));
            Assert.That(item.IsCompleted, Is.False);
        }

        [Test]
        public void AddTodo_NonPositiveIconKeepsTodoDefault()
        {
            string id = _todo.AddTodo(new TodoDraft { Title = "Harvest", IconItemId = 0 });
            Assert.That(_manager.GetTodoById(id).IconItemId, Is.EqualTo(-1));
        }

        [Test]
        public void AddTodo_UnknownPriorityThrows()
        {
            Assert.Throws<ArgumentException>(() => _todo.AddTodo(new TodoDraft { Title = "x", Priority = "Whenever" }));
        }

        [Test]
        public void SetCompleted_TogglesOnlyWhenStateDiffers()
        {
            string id = _todo.AddTodo(new TodoDraft { Title = "Gift Lynn today" });

            Assert.That(_todo.SetCompleted(id, false), Is.False);
            Assert.That(_todo.SetCompleted(id, true), Is.True);
            Assert.That(_manager.GetTodoById(id).IsCompleted, Is.True);
            Assert.That(_todo.SetCompleted(id, true), Is.False);
            Assert.That(_todo.IsCompleted(id), Is.True);
            Assert.That(_todo.SetCompleted(id, false), Is.True);
            Assert.That(_manager.GetTodoById(id).IsCompleted, Is.False);
        }

        [Test]
        public void UnknownId_ReportsNotFound()
        {
            Assert.That(_todo.IsCompleted("missing"), Is.Null);
            Assert.That(_todo.SetCompleted("missing", true), Is.False);
            Assert.That(_todo.RemoveTodo("missing"), Is.False);
            Assert.That(_todo.GetById("missing"), Is.Null);
            Assert.That(_todo.IsCompleted(null), Is.Null);
        }

        [Test]
        public void RemoveTodo_RemovesExistingTodo()
        {
            string id = _todo.AddTodo(new TodoDraft { Title = "Give Claude a birthday gift!" });
            Assert.That(_todo.RemoveTodo(id), Is.True);
            Assert.That(_manager.GetTodoById(id), Is.Null);
        }

        [Test]
        public void GetTodos_SnapshotsAndFiltersCompleted()
        {
            string done = _todo.AddTodo(new TodoDraft { Title = "Done", Description = "Auto-generated by Gifting Assistant", Priority = "Urgent" });
            string open = _todo.AddTodo(new TodoDraft { Title = "Open" });
            _todo.SetCompleted(done, true);

            var all = _todo.GetTodos(activeOnly: false);
            var active = _todo.GetTodos(activeOnly: true);

            Assert.That(all, Has.Count.EqualTo(2));
            Assert.That(active, Has.Count.EqualTo(1));
            Assert.That(active[0].Id, Is.EqualTo(open));

            TodoSnapshot snap = _todo.GetById(done);
            Assert.That(snap.Title, Is.EqualTo("Done"));
            Assert.That(snap.Description, Is.EqualTo("Auto-generated by Gifting Assistant"));
            Assert.That(snap.IsCompleted, Is.True);
            Assert.That(snap.Priority, Is.EqualTo("Urgent"));
        }

        [Test]
        public void GetTodos_ResultSurvivesRemovingWhileIterating()
        {
            _todo.AddTodo(new TodoDraft { Title = "a" });
            _todo.AddTodo(new TodoDraft { Title = "b" });

            foreach (var item in _todo.GetTodos(activeOnly: false))
                _todo.RemoveTodo(item.Id);

            Assert.That(_todo.GetTodos(activeOnly: false), Is.Empty);
        }

        [Test]
        public void GetTodo_DelegatesToGetTodoById()
        {
            string id = _todo.AddTodo(new TodoDraft { Title = "Delegate" });
            Assert.That(_manager.GetTodo(id), Is.SameAs(_manager.GetTodoById(id)));
            Assert.That(_manager.GetTodo(null), Is.Null);
            Assert.That(_manager.GetTodo(""), Is.Null);
            Assert.That(_manager.GetTodo("missing"), Is.Null);
        }
    }
}
