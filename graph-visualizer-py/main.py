TEST_FILE_PATH = r"C:\Users\conta\Desktop\csharp-codebase-tests\SystemUnderTest2.zip"
TEST_ENDPOINT = "http://localhost:5014/api/Analysis/upload"

import requests
import os
from pydantic import BaseModel, Field
from typing import List, Optional, Any
from datetime import datetime


class Node(BaseModel):
    id: str
    label: str
    name: str
    full_name: str = Field(alias='fullName')

class Edge(BaseModel):
    source: str
    target: str
    type: str

class LogEntry(BaseModel):
    timestamp: datetime
    message: str
    data: Optional[Any] = None

class ArtifactMetadata(BaseModel):
    run_id: str = Field(alias='runId')
    source_path: str = Field(alias='sourcePath')
    start_time: datetime = Field(alias='startTime')
    end_time: Optional[datetime] = Field(default=None, alias='endTime')

class AnalysisArtifact(BaseModel):
    metadata: ArtifactMetadata
    log_entries: List[LogEntry] = Field(alias='logEntries')

class AnalysisResult(BaseModel):
    message: str
    source_path: str = Field(alias='sourcePath')
    discovered_solution: Optional[str] = Field(default=None, alias='discoveredSolution')
    discovered_projects: List[str] = Field(alias='discoveredProjects')
    analyzed_project_count: int = Field(alias='analyzedProjectCount')
    collected_triples_count: int = Field(alias='collectedTriplesCount')
    deduplicated_triples_count: int = Field(alias='deduplicatedTriplesCount')
    nodes: List[Node]
    edges: List[Edge]
    artifact: AnalysisArtifact


def upload_zip_file(file_path, url):
    """
    Uploads a zip file to the specified URL.

    :param file_path: The path to the zip file.
    :param url: The URL to upload the file to.
    """
    with open(file_path, 'rb') as f:
        files = {'zipFile': (os.path.basename(file_path), f, 'application/zip')}
        response = requests.post(url, files=files)
        return response

if __name__ == "__main__":
    response = upload_zip_file(TEST_FILE_PATH, TEST_ENDPOINT)
    print(f"Status Code: {response.status_code}")
    if response.ok:
        try:
            analysis_result = AnalysisResult.model_validate_json(response.text)
            print("Successfully parsed analysis result.")
            print(f"Found {len(analysis_result.nodes)} nodes and {len(analysis_result.edges)} edges.")
        except Exception as e:
            print(f"Failed to parse response: {e}")


