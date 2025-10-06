# File Upload API Documentation

## Endpoint Details
- **URL**: `https://manifest.morrenus.xyz/api/v1/upload`
- **Method**: `POST`
- **Content-Type**: `multipart/form-data`
- **Authentication**: Required (API key)

---

## Accepted Files

### Filename Patterns (STRICT)
Only these exact patterns are accepted:

1. **App-specific keys**: `app_<number>_keys.txt`
   - Example: `app_4000_keys.txt`

2. **App-specific tokens**: `app_<number>_token.txt`
   - Example: `app_4000_token.txt`

3. **Universal keys**: `<number>_keys.txt`
   - Example: `54625583_keys.txt`

4. **Universal apps**: `<number>_apps.txt`
   - Example: `54625583_apps.txt`

### File Requirements
- ✅ Must be a `.txt` file
- ✅ Must contain valid UTF-8 text
- ✅ Content-Type should be `text/plain`
- ✅ Must follow the correct format for each file type (see below)
- ❌ Binary files will be rejected
- ❌ Non-text files will be rejected
- ❌ Invalid lines will be automatically removed

### Content Format Requirements

Each file type must follow a specific format. **Invalid lines are automatically removed during upload.**

#### `_keys.txt` Files
**Format:** `{depot_id};{64_character_hex_key}`

**Example valid lines:**
```
228990;44D8C45CE229A11C4F231A3D2A350EAF80B0D69A8AF938EC7CCCA720F694B0E8
4001;70B50AA1B52C66816A5F3EE61235EDC988520BA54E96913F0BF7FF73C37794DE
4002;A3E3A34828218993DBC70C168DD602EE73FE43B8FEF8CD6458BE59DE953DA3D9
```

**Validation rules:**
- Depot ID must be numeric (digits only)
- Key must be exactly 64 hexadecimal characters (0-9, A-F, a-f)
- Separator must be a semicolon (`;`)

---

#### `_token.txt` Files
**Format:** `{depot_id};{token_number}`

**Example valid lines:**
```
1296730;1097817278931358754
1296731;3896636678119466816
1604270;1075697477965442338
3751200;11570295912332125767
```

**Validation rules:**
- Depot ID must be numeric (digits only)
- Token must be numeric (digits only)
- Separator must be a semicolon (`;`)

---

#### `_apps.txt` Files
**Format:** `{depot_id};{token_number}` (same as `_token.txt`)

**Example valid lines:**
```
1296730;1097817278931358754
1296731;3896636678119466816
1604270;1075697477965442338
```

**Validation rules:**
- Depot ID must be numeric (digits only)
- Token must be numeric (digits only)
- Separator must be a semicolon (`;`)

---

## Authentication

You must provide your API key using one of these methods:

### Method 1: Bearer Token (Recommended)
```http
Authorization: Bearer YOUR_API_KEY
```

### Method 2: X-API-Key Header
```http
X-API-Key: YOUR_API_KEY
```

### Method 3: Query Parameter
```http
?api_key=YOUR_API_KEY
```

---

## Request Examples

### cURL Examples

```bash
# Upload app-specific keys file
curl -X POST "https://manifest.morrenus.xyz/api/v1/upload" \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -F "file=@app_4000_keys.txt"

# Upload app-specific token file
curl -X POST "https://manifest.morrenus.xyz/api/v1/upload" \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -F "file=@app_4000_token.txt"

# Upload universal keys file
curl -X POST "https://manifest.morrenus.xyz/api/v1/upload" \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -F "file=@54625583_keys.txt"

# Upload universal apps file
curl -X POST "https://manifest.morrenus.xyz/api/v1/upload" \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -F "file=@54625583_apps.txt"

# Using full Windows path
curl -X POST "https://manifest.morrenus.xyz/api/v1/upload" \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -F "file=@C:\DepotDumper\app_4000_keys.txt"
```

---

### Python Example

```python
import requests

url = "https://manifest.morrenus.xyz/api/v1/upload"
api_key = "YOUR_API_KEY"

headers = {
    "Authorization": f"Bearer {api_key}"
}

# Upload app_4000_keys.txt
file_path = r"C:\DepotDumper\app_4000_keys.txt"
with open(file_path, "rb") as f:
    files = {"file": ("app_4000_keys.txt", f, "text/plain")}
    response = requests.post(url, headers=headers, files=files)

    if response.status_code == 200:
        print("✅ Upload successful!")
        print(response.json())
    else:
        print(f"❌ Upload failed: {response.status_code}")
        print(response.json())

# Upload multiple files
files_to_upload = [
    r"C:\DepotDumper\app_4000_keys.txt",
    r"C:\DepotDumper\app_4000_token.txt",
    r"C:\DepotDumper\54625583_keys.txt",
    r"C:\DepotDumper\54625583_apps.txt"
]

for file_path in files_to_upload:
    filename = file_path.split("\\")[-1]
    with open(file_path, "rb") as f:
        files = {"file": (filename, f, "text/plain")}
        response = requests.post(url, headers=headers, files=files)
        print(f"{filename}: {response.status_code} - {response.json()}")
```

---

### PowerShell Example

```powershell
# Single file upload
$apiKey = "YOUR_API_KEY"
$filePath = "C:\DepotDumper\app_4000_keys.txt"

$headers = @{
    "Authorization" = "Bearer $apiKey"
}

$form = @{
    file = Get-Item -Path $filePath
}

$response = Invoke-RestMethod -Uri "https://manifest.morrenus.xyz/api/v1/upload" `
    -Method Post `
    -Headers $headers `
    -Form $form

Write-Host "Upload successful!"
$response | ConvertTo-Json

# Multiple files upload
$files = @(
    "C:\DepotDumper\app_4000_keys.txt",
    "C:\DepotDumper\app_4000_token.txt",
    "C:\DepotDumper\54625583_keys.txt",
    "C:\DepotDumper\54625583_apps.txt"
)

foreach ($filePath in $files) {
    $form = @{
        file = Get-Item -Path $filePath
    }

    try {
        $response = Invoke-RestMethod -Uri "https://manifest.morrenus.xyz/api/v1/upload" `
            -Method Post `
            -Headers $headers `
            -Form $form

        Write-Host "✅ $($filePath): Success" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ $($filePath): Failed - $($_.Exception.Message)" -ForegroundColor Red
    }
}
```

---

### C# Example

```csharp
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

public class FileUploader
{
    private static readonly HttpClient client = new HttpClient();
    private const string apiUrl = "https://manifest.morrenus.xyz/api/v1/upload";
    private const string apiKey = "YOUR_API_KEY";

    public static async Task UploadFile(string filePath)
    {
        using (var form = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

            string fileName = Path.GetFileName(filePath);
            form.Add(fileContent, "file", fileName);

            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var response = await client.PostAsync(apiUrl, form);
            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Upload successful: {fileName}");
                Console.WriteLine(responseString);
            }
            else
            {
                Console.WriteLine($"❌ Upload failed: {fileName}");
                Console.WriteLine(responseString);
            }
        }
    }

    public static async Task Main(string[] args)
    {
        string[] files = {
            @"C:\DepotDumper\app_4000_keys.txt",
            @"C:\DepotDumper\app_4000_token.txt",
            @"C:\DepotDumper\54625583_keys.txt",
            @"C:\DepotDumper\54625583_apps.txt"
        };

        foreach (var file in files)
        {
            await UploadFile(file);
        }
    }
}
```

---

## Response Formats

### ✅ Success Response (200 OK)

```json
{
  "status": "success",
  "filename": "app_4000_keys.txt",
  "original_size": 1650,
  "cleaned_size": 1524,
  "valid_lines": 15,
  "invalid_lines_removed": 2,
  "saved_to": "/home/solus/SolusBot/keys/txt files/app_4000_keys.txt",
  "zip_file": "/home/solus/SolusBot/keys/zips/12345_tokenandkeys.zip",
  "zip_size": 4896,
  "timestamp": "2025-10-05T14:32:45.123456"
}
```

**Response Fields:**
- `status`: Always `"success"` when upload succeeds
- `filename`: The uploaded filename
- `original_size`: Original file size in bytes (before cleaning)
- `cleaned_size`: Cleaned file size in bytes (after removing invalid lines)
- `valid_lines`: Number of valid lines found in the file
- `invalid_lines_removed`: Number of invalid lines that were removed
- `saved_to`: Server path where cleaned file was saved
- `zip_file`: Path to the user's combined zip file (contains all uploaded files)
- `zip_size`: Size of the zip file in bytes
- `timestamp`: ISO 8601 timestamp of when upload completed

**Note:** If invalid lines were found, they are automatically removed. The cleaned file only contains valid lines.

---

### ❌ Error Responses

#### Invalid Filename (400 Bad Request)

```json
{
  "detail": "Invalid filename. Only files matching patterns 'app_<number>_keys.txt', 'app_<number>_token.txt', '<number>_keys.txt', or '<number>_apps.txt' are accepted."
}
```

**Examples of rejected filenames:**
- ❌ `app_4000.txt` (missing type suffix)
- ❌ `game_4000_keys.txt` (wrong prefix)
- ❌ `app_abc_keys.txt` (non-numeric app ID)
- ❌ `54625583_tokens.txt` (wrong suffix - should be `apps` or `keys`)
- ❌ `app_4000_keys.pdf` (wrong extension)
- ❌ `random_file.txt` (doesn't match any pattern)

---

#### No Valid Lines (400 Bad Request)

```json
{
  "detail": "File contains no valid lines. Expected format not found. Invalid lines: 15"
}
```

**Triggered by:**
- File contains only invalid format lines
- No lines match the expected format for the file type
- All lines were removed during validation

**Example of invalid content in `app_4000_keys.txt`:**
```
depot_id:12345  (wrong separator - should be semicolon)
228990;SHORTKEY  (key too short - must be 64 hex chars)
invalid data here
```

---

#### Non-Text File (400 Bad Request)

```json
{
  "detail": "Only text files are accepted."
}
```

**Triggered by:**
- Files with content type other than `text/*`
- PDFs, images, executables, etc.

---

#### Invalid Encoding (400 Bad Request)

```json
{
  "detail": "File must contain valid UTF-8 text."
}
```

**Triggered by:**
- Binary files disguised with `.txt` extension
- Files with invalid character encodings

---

#### Missing API Key (401 Unauthorized)

```json
{
  "detail": "API key required. Provide via Authorization header (Bearer token), X-API-Key header, or api_key query parameter."
}
```

---

#### Invalid API Key (401 Unauthorized)

```json
{
  "detail": "Invalid API key"
}
```

---

#### Access Forbidden (403 Forbidden)

```json
{
  "detail": "User account is blocked"
}
```

**OR**

```json
{
  "detail": "No access to this endpoint"
}
```

---

#### Rate Limit Exceeded (429 Too Many Requests)

```json
{
  "detail": "Rate limit exceeded: 61/60 requests per minute"
}
```

**OR**

```json
{
  "detail": "Burst rate limit exceeded: 121/120 requests in 60 seconds"
}
```

**OR**

```json
{
  "detail": "Daily API limit exceeded. Used: 1001/1000"
}
```

---

#### Server Error (500 Internal Server Error)

```json
{
  "detail": "Error saving file: [error details]"
}
```

**OR**

```json
{
  "detail": "Internal server error during file upload"
}
```

---

## Rate Limits

The upload endpoint is subject to the same rate limits as other API endpoints:

- **Per-minute limit**: Typically 60 requests/minute
- **Burst limit**: Typically 120 requests/60 seconds
- **Daily limit**: Varies by user role (check `/api/v1/user/stats`)

Check your current usage:
```bash
curl -H "Authorization: Bearer YOUR_API_KEY" \
  https://manifest.morrenus.xyz/api/v1/user/stats
```

---

## File Storage

### Server Directory Structure

All uploaded files are stored on the server in the following locations:

```
/home/solus/SolusBot/keys/
├── txt files/          # Individual cleaned text files
│   ├── app_4000_keys.txt
│   ├── app_4000_token.txt
│   ├── 54625583_keys.txt
│   └── 54625583_apps.txt
└── zips/              # Per-user zip archives
    └── {user_id}_tokenandkeys.zip
```

### Individual Text Files
**Location:** `/home/solus/SolusBot/keys/txt files/`

- Each uploaded file is validated and cleaned
- Invalid lines are automatically removed
- Only valid, cleaned files are saved
- **Note:** If you upload the same filename twice, it will **overwrite** the previous file

### User Zip Archive
**Location:** `/home/solus/SolusBot/keys/zips/{user_id}_tokenandkeys.zip`

- Automatically created/updated after each upload
- Contains **all** text files from the `txt files/` directory
- Named using your user ID (e.g., `12345_tokenandkeys.zip`)
- Recreated each time you upload a new file
- Provides a convenient single download of all your uploaded keys and tokens

---

## Complete Upload Script Example

### Batch Upload Script (Python)

```python
#!/usr/bin/env python3
"""
Batch upload script for depot keys and token files
"""

import os
import requests
from pathlib import Path

API_URL = "https://manifest.morrenus.xyz/api/v1/upload"
API_KEY = "YOUR_API_KEY"  # Replace with your actual API key
SOURCE_DIR = r"C:\DepotDumper\bin\Debug\net9.0-windows"

def upload_file(file_path):
    """Upload a single file to the API"""
    headers = {
        "Authorization": f"Bearer {API_KEY}"
    }

    filename = os.path.basename(file_path)

    try:
        with open(file_path, "rb") as f:
            files = {"file": (filename, f, "text/plain")}
            response = requests.post(API_URL, headers=headers, files=files)

            if response.status_code == 200:
                data = response.json()
                print(f"✅ {filename}: Uploaded ({data['size']} bytes)")
                return True
            else:
                error = response.json().get('detail', 'Unknown error')
                print(f"❌ {filename}: Failed - {error}")
                return False

    except Exception as e:
        print(f"❌ {filename}: Exception - {str(e)}")
        return False

def main():
    """Find and upload all matching files"""
    source_path = Path(SOURCE_DIR)

    if not source_path.exists():
        print(f"❌ Directory not found: {SOURCE_DIR}")
        return

    # Find all matching files
    patterns = [
        "app_*_keys.txt",
        "app_*_token.txt",
        "*_keys.txt",
        "*_apps.txt"
    ]

    files_to_upload = []
    for pattern in patterns:
        files_to_upload.extend(source_path.glob(pattern))

    if not files_to_upload:
        print(f"No matching files found in {SOURCE_DIR}")
        return

    print(f"Found {len(files_to_upload)} file(s) to upload\n")

    # Upload each file
    success_count = 0
    for file_path in files_to_upload:
        if upload_file(file_path):
            success_count += 1

    print(f"\n📊 Summary: {success_count}/{len(files_to_upload)} files uploaded successfully")

if __name__ == "__main__":
    main()
```

---

## Testing the Endpoint

### Quick Test

```bash
# Create a test file
echo "test content" > app_1234_keys.txt

# Upload it
curl -X POST "https://manifest.morrenus.xyz/api/v1/upload" \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -F "file=@app_1234_keys.txt"

# Expected response:
# {"status":"success","filename":"app_1234_keys.txt","size":13,"saved_to":"...","timestamp":"..."}
```

### Test Invalid Files

```bash
# This should fail (wrong pattern)
echo "test" > invalid_file.txt
curl -X POST "https://manifest.morrenus.xyz/api/v1/upload" \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -F "file=@invalid_file.txt"

# Expected: 400 error with detail about invalid filename
```

---

## How Validation Works

When you upload a file, the API performs these steps:

1. **Filename Validation**: Checks if filename matches accepted patterns
2. **Content Type Check**: Verifies file is text (not binary)
3. **UTF-8 Validation**: Ensures file is valid UTF-8 encoded text
4. **Format Validation**: Validates each line against the expected format:
   - `_keys.txt`: Must match `{depot_id};{64_hex_key}`
   - `_token.txt`: Must match `{depot_id};{token_number}`
   - `_apps.txt`: Must match `{depot_id};{token_number}`
5. **Line Cleaning**: Removes all invalid lines automatically
6. **Empty Check**: Rejects file if no valid lines remain
7. **File Save**: Saves cleaned file to `/home/solus/SolusBot/keys/txt files/`
8. **Zip Creation**: Creates/updates your personal zip archive

**Example:**
```
Original file (5 lines):
228990;44D8C45CE229A11C4F231A3D2A350EAF80B0D69A8AF938EC7CCCA720F694B0E8  ✅ Valid
4001;70B50AA1B52C66816A5F3EE61235EDC988520BA54E96913F0BF7FF73C37794DE  ✅ Valid
invalid line here                                                    ❌ Removed
228990:BADFORMAT                                                      ❌ Removed
4002;TOOSHORT                                                         ❌ Removed

Cleaned file (2 lines):
228990;44D8C45CE229A11C4F231A3D2A350EAF80B0D69A8AF938EC7CCCA720F694B0E8
4001;70B50AA1B52C66816A5F3EE61235EDC988520BA54E96913F0BF7FF73C37794DE

Response:
{
  "valid_lines": 2,
  "invalid_lines_removed": 3
}
```

---

## Security Notes

1. **Authentication Required**: All uploads require a valid API key
2. **Filename Validation**: Only specific patterns are accepted
3. **Content Validation**: Files must be valid UTF-8 text
4. **Format Validation**: Each line is validated against expected format
5. **Automatic Cleaning**: Invalid lines are automatically removed
6. **Rate Limited**: Subject to per-minute, burst, and daily limits
7. **Logging**: All upload attempts are logged with user information
8. **Overwrites**: Same filename will overwrite previous upload

---

## Support

If you encounter issues:
1. Check your API key is valid
2. Verify filename matches one of the accepted patterns
3. Ensure file is valid UTF-8 text
4. Check rate limits via `/api/v1/user/stats`
5. Review error message in response

For additional help, contact the API administrator.
