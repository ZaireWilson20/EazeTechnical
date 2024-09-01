# **Job Scraper API**

## **Overview**

The Job Scraper API is a web service designed to scrape job postings from various websites using Selenium WebDriver. The API allows users to send search queries and receive structured job posting data. The service is built using .NET 6 and employs Entity Framework Core for data persistence.

### **Features**

- Scrapes job listings from the first page of specified websites.
- Provides structured job data in JSON format.
- Implements logging for monitoring scraping operations.
- Middleware for validating JSON request bodies.
- Handles different job fields, including title, location, company, salary, and description.
- Stores scraped data in a PostgreSQL database.
- Returns scraped job results directly in the POST request response.

## **Build and Run Instructions**

### **Prerequisites**

- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- [PostgreSQL](https://www.postgresql.org/download/)
- [ChromeDriver](https://sites.google.com/chromium.org/driver/) installed and available in the system PATH
- [Postman](https://www.postman.com/downloads/) (optional, if you prefer using a GUI to send https requests)

### **Environment Variables**

Ensure you have the following environment variables set up in your system:

- `ConnectionStrings__ScrapedJobPostingsDatabase`: Connection string for your PostgreSQL database.
- `ChromeWebDriverPath`: Path to ChromeDriver, if it's not added to your system PATH.

### **Building the API**

**Clone the repository**


1. **Open a PowerShell terminal and run:**

   ``` powershell
   git clone https://github.com/yourusername/JobScraperApi.git
   cd JobScraperApi
   ```
2. **Restore dependencies and build the project:**

    ```
    dotnet restore
    dotnet build
    ```
3. **Apply database migrations:**
    ```
    dotnet ef database update
    ```

### **Running the API**
To run the API, use the following command in PowerShell:
```
dotnet run
```
The API will be available at https://localhost:5001 by default. You can test the endpoints using tools like Postman or PowerShell.
### **Running the Tests**
1. **Navigate to the test project directory:**

    ```
    cd JobScraperApi.Tests
    ```
2. **Run the tests using the .NET CLI:**

    ```
    dotnet test
    ```
This will execute all unit and integration tests, and the results will be displayed in the terminal.
## **API Overview**
### **Endpoints**
- `POST /scrape/{website}`: Starts a job scraping operation based on the provided parameters and returns the scraped results.
- `GET /scrape/{query_id}`: Retrieves the results of a previous scraping operation by query ID.
### **Example Requests**
#### **Scrape Jobs Using Powershell**
To start a scraping operation and get the results immediately, you can use the following PowerShell command:
```
$body = @{
    Query = "Software Engineer"
    Location = "New York"
    LastNdays = 7
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:5001/scrape/indeed" -Method Post -Body $body -ContentType "application/json"
```
#### **Get Scrape Results Using Powershell**
To retrieve the results of a previous scrape operation by query ID:
```
Invoke-RestMethod -Uri "https://localhost:5001/scrape/1" -Method Get
```
### **Response Structure**
Both the POST and GET requests return a response in the following JSON format:
```
{
  "metadata": {
    "query-id": "1"
  },
  "results": [
    {
      "title": "Budtender",
      "company": "Mile High Dispensary",
      "location": "123 Indica Street, San Francisco, CA 99999",
      "description": "Ipsem Lorem",
      "salary": "15-20 $/hr"
    },
    {
      "title": "Grow Op Manager",
      "company": "Mile High Dispensary",
      "location": "123 Indica Street, San Francisco, CA 99999",
      "description": "Ipsem Lorem",
      "salary": "100k/year"
    }
  ]
}
```
### **Postman Example**
1. **Scrape Jobs**
    - Method: `POST`
    - URL: `https://localhost:5001/scrape/indeed`
    - Body (JSON):
        ```
        {
            "Query": "Software Engineer",
            "Location": "New York",
            "LastNdays": 7
        }
        ```
2. **Get Scrape Results:**
    - Method: `GET`
    - URL: `https://localhost:5001/scrape/1`

## **Design Decisions**
### **JSON Validation Middleware**
**Decision**: Implement a custom middleware component that validates the JSON body sent in POST requests.

**Rationale**: This middleware ensures that only expected keys are included in the request body, matching the properties defined in the JobRequestDto model. It enhances API reliability by preventing invalid data from being processed.

**Alternative**: Validation could have been implemented within the controller actions, but using middleware provides a more centralized and reusable approach.

### **Model Design**
**Decision**: Use data transfer objects (DTOs) like `JobRequestDto`, `JobPostingDto`, `QueryResponseDto` to encapsulate and validate data throughout the API.

**Rationale**: DTOs provide a clear contract for what data is expected and returned by the API, making it easier to maintain and test. They also help separate concerns by keeping the data structure distinct from the business logic.

**Alternative**: Using plain models directly might reduce some boilerplate code but could lead to tightly coupled code and make it harder to manage changes in data structure.

### **Selenium WebDriver with ChromeDriver**
**Decision**: The API uses Selenium WebDriver with ChromeDriver to perform web scraping.

**Rationale**: Selenium provides a robust and flexible way to interact with web pages and extract data. ChromeDriver was chosen due to its compatibility and performance with modern websites.

**Alternative**: Other headless browsers like Puppeteer were considered but ultimately not chosen due to my familiarity with Selenium and its established ecosystem.

### **Entity Framework Core with PostgreSQL**
**Decision**: Entity Framework Core was chosen for data access, with PostgreSQL as the database.

**Rationale**: Entity Framework Core offers a high level of abstraction for database operations, which speeds up development and maintenance. PostgreSQL was chosen for its reliability and advanced features.

**Alternative**: Using raw SQL or Dapper was considered but not chosen due to the additional complexity in managing database operations directly.

### **Custom WebDriver Factory**
**Decision**: A custom factory pattern was implemented to create WebDriver and WebDriverWait instances.

**Rationale**: This design allows for better testability and decouples the WebDriver creation logic from the main business logic, making it easier to mock in unit tests.

**Alternative**: Directly instantiating WebDriver objects in the service class was considered but would have led to tightly coupled code, making testing difficult.

## **TODOs and Potential Improvements**
1. **Handle Website Blockers**
    - Currently, the scraper only processes the first page of results due to unclosable pop-ups that appear when attempting to navigate to subsequent pages. In the future, I'd like to find a solution to bypass or close these pop-ups to enable full-page scraping.
2. **Scraping Multiple Websites**
    - Extend the API to support scraping from multiple job posting websites (e.g., LinkedIn, Glassdoor). Additionally, explore integrating with websites or services that provide actual APIs to return job data directly, reducing the reliance on web scraping when possible.
    - Abstract the scraping logic into separate strategies for each website and API to make it easily extensible.
3. **Caching and Rate Limiting**
    - Implement caching mechanisms to store recent scraping results and reduce load on external websites.
    - Add rate-limiting to prevent overwhelming the target websites and avoid being blocked.
4. **Deployment Pipeline**
    - Set up a CI/CD pipeline using GitHub Actions or other tools to automate testing, building, and deployment.


