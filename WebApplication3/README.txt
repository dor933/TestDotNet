Project Setup & Installation
This guide outlines the steps required to configure and run the solution locally using Visual Studio.

Swagger Endpoints
Swagger UI Address: https://localhost:7274/swagger/index.html / http://localhost:5272/swagger/index.html

Swagger JSON Address: https://localhost:7274/swagger/v1/swagger.json / http://localhost:5272/swagger/v1/swagger.json

Note: These addresses can be edited in Properties/launchSettings.json.

Prerequisites
Port Availability: Ensure that ports 5272 and 7274 are free and available on your local machine.

Configuration
1. Package Setup
Restore the necessary dependencies before building:

Right-click the Solution in the Solution Explorer.

Select Restore NuGet Packages.

2. Database Setup
Before starting the project, you must configure the database connection string:

Open the appsettings.json file.

Locate the database connection string settings.

Update the server address to match your environment (local or remote; the default is usually localhost or .).

Note: If you are using SQL Express, ensure the address is set to .\SQLEXPRESS or <YourMachineName>\SQLEXPRESS.

Note: The database schema and sample data are automatically generated upon startup by the /Database/Init.cs file

3. Documentation File Setup
To ensure the project generates and reads the XML documentation file correctly (preventing "File Not Found" errors), follow these steps:

Right-click your Project and select Properties.

Navigate to Build > Output (or search for "Documentation file" in the search bar).

Verify that the checkbox labeled "Generate a file containing API documentation" is checked.

How to Run (Visual Studio)

To run the system successfully, its better that the server side will be running before the client attempts to connect.

Configure Startup Projects

Right-click on the Solution file in the Solution Explorer.

Select Properties.

Go to Common Properties > Startup Project.

Select Multiple startup projects.

Set the start order as follows:

Web Application 3 (Action: Start)

TCP Client (Action: Start)

Click Apply and OK.

Start the Solution
Press F5 or click the Start button in the top toolbar to launch the solution.