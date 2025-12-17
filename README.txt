swagger UI address: http://localhost:5272/swagger/index.html
swagger JSON address: http://localhost:5272/swagger/v1/swagger.json

Project Setup & Installation

This guide outlines the steps required to configure and run the solution locally using Visual Studio.

Prerequisites

*Port Availability*: Ensure that **port 5272** is free and available on your local machine/server.

Configuration

Database Setup
Before starting the project, you must configure the database connection string:

1. Open the `appsettings.json` file.
2. Locate the database connection string settings.
3. Update the server address to match your local environment.
*Note:* If you are using **SQL Express**, ensure the address is set to `localhost\SQLEXPRESS`.

How to Run (Visual Studio)

To run the system successfully, the startup order of the projects is critical. The server side must be up and running before the client attempts to connect.

### Configure Startup Projects
1. Right-click on the Solution file in the Solution Explorer.
2. Select Properties.
3. Go to Common Properties > Startup Project.
4. Select Multiple startup projects.
5. Set the start order as follows:
    1. Web Application 3 (Action: `Start`)
    2. TCP Client (Action: `Start`)
6. Click Apply and OK.

Important: The `Web Application 3` acts as the server. It must finish initializing before the `TCP Client` runs to establish a successful connection.