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

TEST_FILE_PATH = r"C:\Users\conta\Desktop\csharp-codebase-tests\SystemUnderTest2.zip"
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

    def node_text(node: Node):
        return f"Label: {node.label}\nName: {node.name}\nFull Name: {node.full_name}"

    documents = []
    for node in result.nodes:
        if node.label == "File":
            documents.append(Document(
                page_content=node_text(node),
                metadata={"id": node.id, "label": node.label, "name": node.name, "fullName": node.full_name}
            ))

    from langchain_openai import OpenAIEmbeddings
    embedder = OpenAIEmbeddings(model="text-embedding-3-small", dimensions=DIMENSIONS)

    db = Neo4jVector.from_documents(
        documents, embedder, url="neo4j+ssc://33c68691.databases.neo4j.io", username="neo4j", password="uPKr5GehtVkbhE7BOgByr-2ESZ3SLicvQH9PTcCxc48")

    query = "math"
    docs_with_score = db.similarity_search_with_score(query)

    print("Retrieved documents:")
    for index, result in enumerate(docs_with_score):
        print(f"Document {index}:")
        print(f"  Score: {result[1]}")
        print(f"  Metadata: {result[0].metadata}")
        print(f"  Page Content:\n{result[0].page_content}")
        print("-" * 50)

def main():
    driver = GraphDatabase.driver(
        "neo4j+ssc://33c68691.databases.neo4j.io",
        auth=("neo4j", "uPKr5GehtVkbhE7BOgByr-2ESZ3SLicvQH9PTcCxc48")
    )

    response = upload_zip_file(TEST_FILE_PATH, TEST_ENDPOINT)
    print(f"Status Code: {response.status_code}")
    if response.ok:
        try:
            analysis_result = AnalysisResult.model_validate_json(response.text)
            print("Successfully parsed analysis result.")
            print(f"Found {len(analysis_result.nodes)} nodes and {len(analysis_result.edges)} edges.")

            asyncio.run(populate_database(analysis_result, driver))
        except Exception as e:
            print(f"Failed to parse response: {e}")

    print("Populated database")

    driver.close()


if __name__ == "__main__":
    main()
