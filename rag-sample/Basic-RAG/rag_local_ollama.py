# rag_local_ollama.py

import requests
from sentence_transformers import SentenceTransformer
from qdrant_client import QdrantClient
from qdrant_client.models import PointStruct, VectorParams, Distance

# --- Settings ---
COLLECTION_NAME = "my_documents"
MODEL_NAME = "all-MiniLM-L6-v2"
OLLAMA_URL = "http://localhost:11434"
OLLAMA_MODEL = "llama3"

# --- Sample Documents ---
documents = [
    {"id": 1, "text": "Qdrant is a vector database used for similarity search in machine learning."},
    {"id": 2, "text": "Sentence Transformers are models that convert text into dense vectors for semantic search."},
    {"id": 3, "text": "LLaMA is a family of open-source LLMs developed by Meta for local deployment."}
]

# --- Load Embedding Model ---
model = SentenceTransformer(MODEL_NAME)

# --- Generate Embeddings ---
for doc in documents:
    doc["vector"] = model.encode(doc["text"]).tolist()

# --- Connect to Qdrant ---
client = QdrantClient(host="localhost", port=6333)

# --- Create/Recreate Collection ---
if client.collection_exists(collection_name=COLLECTION_NAME):
    client.delete_collection(collection_name=COLLECTION_NAME)

client.create_collection(
    collection_name=COLLECTION_NAME,
    vectors_config=VectorParams(size=384, distance=Distance.COSINE)
)

# --- Upload to Qdrant ---
points = [
    PointStruct(id=doc["id"], vector=doc["vector"], payload={"text": doc["text"]})
    for doc in documents
]
client.upsert(collection_name=COLLECTION_NAME, points=points)

# --- Ask a Question ---
query = input("Ask a question: ")
query_vector = model.encode(query).tolist()

# --- Search for Similar Documents ---
results = client.search(
    collection_name=COLLECTION_NAME,
    query_vector=query_vector,
    limit=3,
    with_payload=True
)
retrieved_texts = [res.payload["text"] for res in results]
context = "\n".join(retrieved_texts)

# --- Build Prompt for Local LLM ---
prompt = f"""You are a helpful assistant.
Use the context below to answer the question.

Context:
{context}

Question: {query}
Answer:"""

# --- Send to Ollama ---
def ask_ollama(prompt, model=OLLAMA_MODEL):
    response = requests.post(
        f"{OLLAMA_URL}/api/chat",
        json={
            "model": model,
            "messages": [
                {"role": "system", "content": "You are a helpful assistant."},
                {"role": "user", "content": prompt}
            ]
        }
    )
    return response.json()["message"]["content"]

# --- Get Answer ---
answer = ask_ollama(prompt)
print("\n--- Answer ---")
print(answer)
