import json
from qdrant_client import QdrantClient, models
from sentence_transformers import SentenceTransformer # <--- ADD THIS LINE

# --- Configuration ---
QDRANT_HOST = "localhost"
QDRANT_PORT = 6333 # REST API port
COLLECTION_NAME = "my_documents"
VECTOR_SIZE = 384 # This should match the output dimension of your SentenceTransformer model
MODEL_NAME = "all-MiniLM-L6-v2" # <--- ADD THIS LINE (or the model you used for generation)

# --- Load Vectors from File ---
try:
    with open('vectors.json', 'r') as f:
        vectors_data = json.load(f)
except FileNotFoundError:
    print("Error: vectors.json not found. Please run generate_vectors.py first.")
    exit()

# --- Initialize Qdrant Client ---
client = QdrantClient(host=QDRANT_HOST, port=QDRANT_PORT)

# --- Initialize SentenceTransformer Model --- # <--- ADD THIS BLOCK
try:
    model = SentenceTransformer(MODEL_NAME)
    print(f"Loaded SentenceTransformer model: {MODEL_NAME}")
except Exception as e:
    print(f"Error loading SentenceTransformer model: {e}")
    exit()


# --- Create Collection (if it doesn't exist) ---
# ... (rest of your collection creation code)
# Your existing code here is fine, the DeprecationWarning is noted.
try:
    client.recreate_collection(
        collection_name=COLLECTION_NAME,
        vectors_config=models.VectorParams(size=VECTOR_SIZE, distance=models.Distance.COSINE),
    )
    print(f"Collection '{COLLECTION_NAME}' recreated successfully (or created if it didn't exist).")
except Exception as e:
    print(f"Error creating/recreating collection: {e}")
    print("Attempting to get collection info in case it already exists...")
    try:
        collection_info = client.get_collection(collection_name=COLLECTION_NAME)
        print(f"Collection '{COLLECTION_NAME}' already exists with config: {collection_info.config}")
    except Exception as get_e:
        print(f"Could not get collection info either: {get_e}")
        exit()

# --- Prepare Points for Upload ---
# ... (rest of your points preparation code)
points = []
for item in vectors_data:
    points.append(
        models.PointStruct(
            id=item["id"],
            vector=item["vector"],
            payload=item["payload"]
        )
    )

# --- Upload Points to Qdrant ---
# ... (rest of your upload code)
try:
    operation_info = client.upsert(
        collection_name=COLLECTION_NAME,
        wait=True, # Wait for the operation to be completed
        points=points
    )
    print(f"Successfully uploaded {len(points)} vectors to Qdrant.")
    print(f"Operation info: {operation_info}")
except Exception as e:
    print(f"Error uploading vectors: {e}")

# --- Verify (Optional) ---
# ... (rest of your verification code)
try:
    count_result = client.count(
        collection_name=COLLECTION_NAME,
        exact=True # Get exact count
    )
    print(f"Total vectors in collection '{COLLECTION_NAME}': {count_result.count}")
except Exception as e:
    print(f"Error getting collection count: {e}")

# --- Perform a sample search (Optional) ---
print("\n--- Performing a sample search ---")
query_text = "What is a vector database?"
# --- THIS IS THE FIX ---
query_vector = model.encode(query_text).tolist() # Use the 'model' object imported from sentence_transformers

try:
    search_result = client.search(
        collection_name=COLLECTION_NAME,
        query_vector=query_vector,
        limit=2, # Get top 2 results
        with_payload=True # Include the original text in the results
    )
    print(f"Search results for '{query_text}':")
    for hit in search_result:
        print(f"    ID: {hit.id}, Score: {hit.score:.4f}, Text: {hit.payload['text']}")
except Exception as e:
    print(f"Error during search: {e}")