import { expect, test } from "@playwright/test";

test("add, list, complete and delete a todo", async ({ page }) => {
  const title = `todo ${Date.now()}`;
  await page.goto("/");
  await page.getByLabel("New todo").fill(title);
  await page.getByRole("button", { name: "Add" }).click();

  const row = page.getByTestId("todo").filter({ hasText: title });
  await expect(row).toBeVisible();

  await row.getByRole("checkbox").check();
  await expect(row.getByRole("checkbox")).toBeChecked();
  await page.reload();
  await expect(page.getByTestId("todo").filter({ hasText: title }).getByRole("checkbox")).toBeChecked();

  await page.getByRole("button", { name: `Delete ${title}` }).click();
  await expect(page.getByTestId("todo").filter({ hasText: title })).toHaveCount(0);
});

test("a blank todo is not added", async ({ page }) => {
  await page.goto("/");
  const before = await page.getByTestId("todo").count();
  await page.getByRole("button", { name: "Add" }).click();
  await expect(page.getByTestId("todo")).toHaveCount(before);
});
