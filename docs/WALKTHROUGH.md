# A five-minute walkthrough

The shortest route through the project that touches everything it demonstrates: the website, the API, the documentation, the rules, and the tests. Each stop says what to do and what it shows. Stops 1 to 7 take about five minutes; stops 8 and 9 are extras for when there is time. The stops stand alone, so they can be taken in any order.

Before starting: `dotnet run --project src/CraftingPlanner.Api`, then open <http://localhost:5080> in a browser with the developer tools' Network tab open. The hosted copy at <https://crafting-planner.onrender.com> works for every stop too, but it may take a minute to wake and anyone can change its data, so the local run is the dependable choice for a live walkthrough.

## 1. The question the app answers (30 seconds)

On the **Plan** page, pick **Tool Kit**, set the quantity to **3**, and press **Make the plan**.

What it shows: the product. Raw materials to gather (3 Logs, 30 Iron Ore, 15 Coal), six crafting steps in the order to do them, and the total time. Point at the Stick row: 9 required, 3 batches, 12 produced, 3 left over. That rounding is what the design document is built around.

## 2. The same thing through the API (45 seconds)

In the Network tab, click the request that just ran: `GET /api/items/18/plan?quantity=3`. Its response is the JSON the page drew.

Now open <http://localhost:5080/swagger>, find the same operation under **Planning**, choose **Try it out**, enter 18 and 3, **Execute**.

What it shows: the website and the interactive API page are two clients of one backend. Nothing on the site bypasses the API. The API page lists 29 operations in eight groups, and every one of them can be tried this way.

## 3. Insert and update (45 seconds)

On the **Items** page, press **New item**. Name it **Copper Ore**, choose the category **Raw Material**, leave "This item has a recipe" unticked, and press **Create item**. Its page opens. Type a description and press **Save**.

Back on the API page, run `GET /api/items` with `search` = `copper`: the new item is there, description included.

What it shows: create and update, with the page and the API agreeing. One form covers an item and its recipe; ticking the box reveals the recipe fields and the ingredient picker.

## 4. The rules, refused in plain words (60 seconds)

Three rejections, each shown on the page in the words the API sent:

| Do | What comes back |
|---|---|
| On **New item**, name it **stick** and press Create item | 409, "The name is already in use": another item is already named "stick", ignoring letter case |
| Open **Stick**, press **Delete item**, and confirm | 409, "The item is in use": it is an ingredient of Arrow, Bow, Pickaxe, Sword, Torch |
| Open **Plank**, add **Stick** to its ingredients with the picker, press **Save** | 409, "The recipe would loop": "Plank" needs "Stick", and "Stick" needs "Plank" |

What it shows: the backend owns the rules, and the pages simply show what it answers. The loop check finds loops at any depth and names the whole loop; the API guide has a five-deep example.

## 5. Documentation, generated and enforced (45 seconds)

Open any operation on the API page and read its description, its parameters, and its listed responses. Then open `src/CraftingPlanner.Api/Controllers/ItemsController.cs` and show the comment above the same operation: they are the same text.

What it shows: the interactive documentation is generated from the code comments, so it cannot drift. The build refuses to compile if any public member lacks a comment (`WarningsAsErrors` in the project file). To prove it, delete the comment block above one endpoint, run `dotnet build`, read the error, and put the block back.

## 6. Tests and the prototype (45 seconds)

```
dotnet test
py prototype/test_plan.py
```

What it shows: 91 automated tests, including ones that start the real app on a fresh database and call its endpoints the way a client would. The planning calculation was written in Python first; the C# version is held to the same five cases and the same numbers, and the 39 Python checks still pass.

## 7. The plan behind it (30 seconds)

Open `docs/DESIGN.md`. Show the "Deliberately left out" table, the "Key decision: total the demand before rounding" example, and the "Added after the planned stages" table.

What it shows: scope was chosen, not run out of. The rounding trap was found and solved at the design stage, before any code. The three features added after the six planned stages (item pictures, the picker, saved lists) were each given a reason and a pull request of their own.

## 8. Lists, if there is time (45 seconds)

On the **Lists** page, create **Starter gear**. On its page, add a **Sword** with quantity 2 and a **Pickaxe** with quantity 1 from the picker. The plan underneath redraws after each change.

What it shows: the same calculation applied to several items at once, with shared ingredients totalled across the whole list before rounding: 4 Sticks in all is one batch of four, nothing left over. Now add a **Tool Kit**: the Sword row goes to 3 required (two from the list, one inside the Tool Kit) and the Stick row to 7 required, 2 batches, 1 left over. **Download plan as CSV** gives the same plan as a file.

## 9. The hosted copy, if there is time (30 seconds)

Open <https://crafting-planner.onrender.com>. The notice under the header says it is a public demo. Open `/api/about` on it: `demoMode` is true.

What it shows: the same code running in a container built from the `Dockerfile`, with one setting turned on. Demo mode resets the data on every start, rate-limits the API, and shows the notice; a local run never has it. The design document's "Hosting and the public demo" section lists the risks of a public demo and what demo mode does about each.

## If something goes wrong

| Problem | Fix |
|---|---|
| Port 5080 is in use | Another copy is running. Stop it |
| The data looks changed | Stop the app, delete `src/CraftingPlanner.Api/craftingplanner.db`, start again |
| The app stops at start and says the database file is from an older version | Same fix: delete the file. The sample data is rebuilt on start |
| No browser at hand | `curl http://localhost:5080/api/items/18/plan?quantity=3` shows the same plan |
| The hosted copy does not answer | It is waking up. Wait a minute and reload |
