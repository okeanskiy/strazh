## PrecisionApi.Controllers.AnalysisController

**Purpose:** Handles incoming HTTP requests for code analysis.

**Current State (as of last update):**

*   **Route:** `api/Analysis`
*   **Dependencies:** 
    *   `PrecisionApi.Services.AnalysisService` (injected via constructor).
*   **Endpoints:**
    *   **`POST api/Analysis/upload`**
        *   **Request:** Expects `IFormFile` named `zipFile` (multipart/form-data).
        *   **Functionality:**
            1.  Validates if a file is uploaded and if it's a `.zip` file.
            2.  Creates a unique temporary directory (e.g., under `Path.GetTempPath()/PrecisionApi_Uploads/GUID`).
            3.  Saves the uploaded `.zip` file to this temporary directory.
            4.  Extracts the contents of the `.zip` file into the same temporary directory.
            5.  Deletes the original `.zip` file from the temporary directory after extraction.
            6.  Calls the injected `_analysisService.AnalyzeCodebaseAsync(tempExtractPath)` method, passing the path to the directory where files were extracted.
            7.  Returns the `JsonDocument` received from the `AnalysisService` as an `OkObjectResult`.
        *   **Error Handling:** Includes a `try-catch` block. On exception, logs to console (basic) and returns a `StatusCode(500, ex.Message)`.
        *   **Cleanup (Finally Block):** Contains comments about robust temporary directory cleanup but currently does not automatically delete the `tempExtractPath` to allow inspection during development.
        *   **Produces:**
            *   `200 OK` with `JsonDocument` (the graph).
            *   `400 Bad Request` for invalid input.
            *   `500 Internal Server Error` for processing errors.
*   **Notable Placeholders/TODOs (in Controller):** 
    *   More robust logging (e.g., using `ILogger`).
    *   Refined temporary directory cleanup strategy for production. 