## 🛠️ Bot Setup


### 1. Configure the Database URL

Open the following file:

[Database/BotDbContext.cs](/Database/BotDbContext.cs#L15)

Locate the `OnConfiguring` method and change the 'Database=CountingBotDb' to whatever you named it and change the username and password.

---

### 2. Initialize the Database

Open a terminal and **navigate to the root directory of the project** where your `.csproj` file is located.

Then run the following commands:

```bash
# Create the initial migration
dotnet ef migrations add InitialCreate

# Apply the migration to create the database
dotnet ef database update
```

### 3. Running the Bot

Now that the setup is complete, you can start the bot by running the following command in your terminal (from the root project directory):

```bash
dotnet run
````

> 💡 This assumes you're in the same directory as the `.csproj` file. If your project is in a subfolder, navigate there first using `cd`.
