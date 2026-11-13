import { expect, test } from "@playwright/test";

// Two previews at once (Done 3). BASE_URL is one preview, OTHER_URL another.
// CHANGE_TEXT is text that a visible change made on the OTHER branch adds to the page.
const other = process.env.OTHER_URL;
const changeText = process.env.CHANGE_TEXT;

test.skip(!other || !process.env.BASE_URL, "needs BASE_URL and OTHER_URL");

test("a todo added in one preview does not appear in the other", async ({ page, browser }) => {
  const title = `only here ${Date.now()}`;
  await page.goto("/");
  await page.getByLabel("New todo").fill(title);
  await page.getByRole("button", { name: "Add" }).click();
  await expect(page.getByText(title)).toBeVisible();

  const otherPage = await browser.newPage();
  await otherPage.goto(other!);
  await expect(otherPage.getByRole("heading")).toBeVisible();
  await otherPage.reload();
  await expect(otherPage.getByText(title)).toHaveCount(0);

  await page.getByRole("button", { name: `Delete ${title}` }).click();
  await otherPage.close();
});

test("a visible change on one branch shows only at that branch's URL", async ({ page, browser }) => {
  test.skip(!changeText, "needs CHANGE_TEXT");
  await page.goto("/");
  await expect(page.getByRole("heading")).toBeVisible();
  await expect(page.getByText(changeText!)).toHaveCount(0);

  const otherPage = await browser.newPage();
  await otherPage.goto(other!);
  await expect(otherPage.getByText(changeText!)).toBeVisible();
  await otherPage.close();
});
