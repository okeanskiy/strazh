import requests
import os
from pydantic import BaseModel, Field
from typing import List, Optional, Any
from datetime import datetime

from neo4j import GraphDatabase
import asyncio

from neo4j_graphrag.experimental.components.kg_writer import Neo4jWriter
from neo4j_graphrag.experimental.components.types import (
    Neo4jNode,
    Neo4jRelationship,
    Neo4jGraph,
    LexicalGraphConfig
)

TEST_FILE_PATH = r"C:\Users\conta\Desktop\csharp-codebase-tests\SystemUnderTest2.zip"
TEST_ENDPOINT = "http://localhost:5014/api/Analysis/upload"

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

def clear_database(driver: GraphDatabase.driver):
    with driver.session() as session:
        session.run("MATCH (n) DETACH DELETE n")
    driver.close()
    print("✅ Database cleared.")

async def test_neo4j(result: AnalysisResult):
    from collections import Counter
    id_counts = Counter(n.id for n in result.nodes)
    duplicate_ids = [id for id, count in id_counts.items() if count > 1]
    if duplicate_ids:
        raise ValueError(f"Duplicate IDs found: {duplicate_ids}")
    else:
        print("No duplicate IDs found.")

    driver = GraphDatabase.driver(
        "neo4j+ssc://33c68691.databases.neo4j.io",
        auth=("neo4j", "uPKr5GehtVkbhE7BOgByr-2ESZ3SLicvQH9PTcCxc48")
    )

    clear_database(driver)

    writer = Neo4jWriter(driver)

    neo4j_nodes = [
        Neo4jNode(
            id=n.id,
            label=n.label,
            properties={"name": n.name, "fullName": n.full_name}
        )
        for n in result.nodes
    ]

    neo4j_relationships = [
        Neo4jRelationship(
            start_node_id=e.source,
            end_node_id=e.target,
            type=e.type,
            properties={
                # potential place to put raw source code
            }
        )
        for e in result.edges
    ]

    graph = Neo4jGraph(nodes=neo4j_nodes, relationships=neo4j_relationships)

    result = await writer.run(graph)
    print(f"Write status: {result.status}")

    driver.close()

if __name__ == "__main__":
    response = upload_zip_file(TEST_FILE_PATH, TEST_ENDPOINT)
    print(f"Status Code: {response.status_code}")
    if response.ok:
        try:
            analysis_result = AnalysisResult.model_validate_json(response.text)
            print("Successfully parsed analysis result.")
            print(f"Found {len(analysis_result.nodes)} nodes and {len(analysis_result.edges)} edges.")

            asyncio.run(test_neo4j(analysis_result))
        except Exception as e:
            print(f"Failed to parse response: {e}")
