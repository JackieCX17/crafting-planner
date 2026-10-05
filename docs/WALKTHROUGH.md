# A five-minute walkthrough

The shortest route through the project that touches everything it demonstrates: the website, the API, the documentation, the rules, and the tests. Each step says what to do and what it shows.

Before starting: `dotnet run --project src/CraftingPlanner.Api`, and open <http://localhost:5080> in a browser with the developer tools' Network tab open. (The public demo works for this too, but it may take a minute to wake and its data may have been changed by someone else; the local run is the dependable choice for a live walkthrough.)

## 1. The question the app answers (30 seconds)

On the **Plan** page, pick **Tool Kit**, set the quantity to **3**, and press the button.

What it shows: the product. Raw materials to gather, six crafting steps in the order to do them, and the total time. Point at the Stick row: 9 needed, 3 crafts, 12 made, 3 left over. That's the rounding the design document is built around.

## 2. The same thing through the API (45 seconds)

In the Network tab, click the request that just ran: `GET /api/items/18/plan?quantity=3`. Its response is the same JSON the page drew.

Now open <http://localhost:5080/swagger>, find the same endpoint under **Planning**, choose **Try it out**, enter 18 and 3, **Execute**.

What it shows: the website and the interactive API page are two clients of one backend. Nothing on the site bypasses the API.

## 3. Insert and update (45 seconds)

On the **Items** page, add an item named **Copper Ore**, category **Raw Material**. Its page opens. Change the description and save.

Back on Swagger, `GET /api/items` with `search` = `copper`: the new item is there.

What it shows: create and update, with the page and the API agreeing.

## 4. The rules, refused in plain words (60 seconds)

Three rejections, each visible on the page:

| Do | What comes back |
|---|---|
| On the Items page, add another item named **stick** | 409: the name is already in use, ignoring letter case |
| On the Items page, delete **Stick** | 409: it is an ingredient of Arrow, Bow, Pickaxe, Sword, Torch |
| Open **Plank**, edit its recipe to use a **Stick**, save | 409: "Plank" needs "Stick", and "Stick" needs "Plank" |

What it shows: the backend owns the rules, and the pages simply show what it answers. The loop check finds loops at any depth; the API guide has a five-deep example.

## 5. Documentation, generated and enforced (45 seconds)

Open any endpoint in Swagger and read its description and its listed responses. Then open `src/CraftingPlanner.Api/Controllers/ItemsController.cs` and show the comment above the same endpoint: they are the same text.

What it shows: the interactive documentation is generated from the code comments, so it cannot drift. The build refuses to compile if any public member lacks a comment (`WarningsAsErrors` in the project file).

## 6. Tests and the prototype (45 seconds)

```
dotnet test
py prototype/test_plan.py
```

What it shows: 69 automated tests, including ones that start the real app and call its endpoints. The planning calculation was written in Python first; the C# version is held to the same five cases and the same numbers.

## 7. The plan behind it (30 seconds)

Open `docs/DESIGN.md`. Show the "Deliberately left out" table and the "Key decision: total the demand before rounding" example.

What it shows: scope was chosen, not run out of. The rounding trap was found and solved at the design stage, before any code.

## 8. Lists, if there is time (45 seconds)

On the **Lists** page, create "Starter gear". On its page, add a Sword with quantity 2 and a Pickaxe with quantity 1 from the picker. The plan underneath redraws after each change.

What it shows: the same calculation applied to several items at once, with shared ingredients totalled across the whole list before rounding: 4 Sticks in all is one craft of four, nothing left over. Point out that adding a Tool Kit to the same list counts the Sword once, with both shares added.

## If something goes wrong

| Problem | Fix |
|---|---|
| Port 5080 is in use | Another copy is running. Stop it |
| The data looks changed | Stop the app, delete `src/CraftingPlanner.Api/craftingplanner.db`, start again |
| No browser at hand | `curl http://localhost:5080/api/items/18/plan?quantity=3` shows the same plan |
