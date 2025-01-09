# SAFE Template

This application was created using the [SAFE Template](https://safe-stack.github.io/docs/template-overview/). It serves as a dashboard for visualizing power system data from Elering.

## Install pre-requisites

You'll need to install the following pre-requisites in order to build SAFE applications:

* [.NET SDK](https://www.microsoft.com/net/download) 8.0 or higher
* [Node 18](https://nodejs.org/en/download/) or higher
* [NPM 9](https://www.npmjs.com/package/npm) or higher

## Starting the application

To concurrently run the server and the client components in watch mode use the following command:

```bash
dotnet run
```
Then open a browser and navigate to `http://localhost:8080` to see the site.


## Application Overview

This application is a dashboard for visualizing power system data from Elering. It displays various metrics such as production, consumption, losses, frequency, system balance, AC balance, production renewable, and solar energy production over time.