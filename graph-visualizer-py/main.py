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

from neo4j_graphrag.retrievers import VectorRetriever
from neo4j_graphrag.llm import OpenAILLM
from neo4j_graphrag.generation import GraphRAG
from neo4j_graphrag.embeddings import OpenAIEmbeddings
from neo4j_graphrag.types import EntityType
from neo4j_graphrag.indexes import create_vector_index, upsert_vectors, drop_index_if_exists

from neo4j_graphrag.indexes import retrieve_vector_index_info

from langchain.docstore.document import Document
from langchain_neo4j import Neo4jVector

# TEST_FILE_PATH = r"C:\Users\conta\Desktop\csharp-codebase-tests\SystemUnderTest2.zip"
TEST_FILE_PATH = r"C:\Users\conta\Desktop\csharp-codebase-tests\nopCommerce-develop.zip"
TEST_ENDPOINT = "http://localhost:5014/api/Analysis/upload"

INDEX_NAME = "file-vector-index"
EMBEDDING_PROPERTY = "embedding"
DIMENSIONS = 384
DATABASE = "neo4j"

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
    print("✅ Database cleared.")

async def populate_database(result: AnalysisResult, driver: GraphDatabase.driver):
    clear_database(driver)

    neo4j_nodes = [
        Neo4jNode(
            id=node.id,
            label=node.label,
            properties={
                "name": node.name,
                "fullName": node.full_name,
                "analysisId": node.analysis_id
            },
        )
        for node in result.nodes
    ]

    neo4j_relationships = [
        Neo4jRelationship(
            source_id=edge.source,
            target_id=edge.target,
            type=edge.type,
        )
        for edge in result.edges
    ]

    graph = Neo4jGraph(nodes=neo4j_nodes, relationships=neo4j_relationships)
    writer = Neo4jWriter(driver, database=DATABASE)
    await writer.write_graph(graph)
    print(f"Successfully populated database with {len(neo4j_nodes)} nodes and {len(neo4j_relationships)} relationships.")

def main():
    driver = GraphDatabase.driver(
        "neo4j+ssc://33c68691.databases.neo4j.io",
        auth=("neo4j", "uPKr5GehtVkbhE7BOgByr-2ESZ3SLicvQH9PTcCxc48")
    )

    try:
        response = upload_zip_file(TEST_FILE_PATH, TEST_ENDPOINT)
        print(f"Status Code: {response.status_code}")
        if response.ok:
            try:
                analysis_result = AnalysisResult.model_validate_json(response.text)
                print("Successfully parsed analysis result.")
                print(f"Found {len(analysis_result.nodes)} nodes and {len(analysis_result.edges)} edges.")

                asyncio.run(populate_database(analysis_result, driver))
                print("Populated database")
            except Exception as e:
                print(f"Failed to parse response: {e}")

        else:
            print(f"Failed to upload file: {response.text}")

    finally:
        driver.close()


if __name__ == "__main__":
    main()
