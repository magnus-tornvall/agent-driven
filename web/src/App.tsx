import { createResource, createSignal, For, Show } from "solid-js";
import { addTodo, deleteTodo, listTodos, setCompleted } from "./api";

export function App() {
  const [todos, { refetch }] = createResource(listTodos);
  const [title, setTitle] = createSignal("");
  const [error, setError] = createSignal("");

  const run = async (action: () => Promise<unknown>) => {
    setError("");
    try {
      await action();
      await refetch();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  const submit = (e: SubmitEvent) => {
    e.preventDefault();
    const t = title().trim();
    if (!t) return;
    setTitle("");
    void run(() => addTodo(t));
  };

  return (
    <main style={{ "max-width": "32rem", margin: "2rem auto", "font-family": "system-ui" }}>
      <h1>Todos</h1>
      <form onSubmit={submit}>
        <input
          aria-label="New todo"
          placeholder="What needs doing?"
          value={title()}
          onInput={(e) => setTitle(e.currentTarget.value)}
        />
        <button type="submit">Add</button>
      </form>
      <Show when={error()}>
        <p role="alert" style={{ color: "crimson" }}>{error()}</p>
      </Show>
      <Show when={todos.error}>
        <p role="alert" style={{ color: "crimson" }}>Could not load todos.</p>
      </Show>
      <ul aria-label="Todo list">
        <For each={todos()}>
          {(t) => (
            <li data-testid="todo">
              <label>
                <input
                  type="checkbox"
                  checked={t.completed}
                  onChange={(e) => void run(() => setCompleted(t.id, e.currentTarget.checked))}
                />
                <span style={{ "text-decoration": t.completed ? "line-through" : "none" }}>{t.title}</span>
              </label>
              <button aria-label={`Delete ${t.title}`} onClick={() => void run(() => deleteTodo(t.id))}>
                Delete
              </button>
            </li>
          )}
        </For>
      </ul>
    </main>
  );
}
