export type Todo = { id: string; title: string; completed: boolean };

async function call(path: string, init?: RequestInit): Promise<Response> {
  const res = await fetch(`/api/todos${path}`, {
    ...init,
    headers: { "Content-Type": "application/json" },
  });
  if (!res.ok) throw new Error(`${init?.method ?? "GET"} /api/todos${path} failed: ${res.status}`);
  return res;
}

export const listTodos = async (): Promise<Todo[]> => (await call("/")).json();
export const addTodo = async (title: string): Promise<Todo> =>
  (await call("/", { method: "POST", body: JSON.stringify({ title }) })).json();
export const setCompleted = async (id: string, completed: boolean): Promise<Todo> =>
  (await call(`/${id}`, { method: "PUT", body: JSON.stringify({ completed }) })).json();
export const deleteTodo = async (id: string): Promise<void> => {
  await call(`/${id}`, { method: "DELETE" });
};
