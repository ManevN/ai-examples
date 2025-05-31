from sentence_transformers import SentenceTransformer
import json 

# Load embedding model
model = SentenceTransformer('all-MiniLm-L6-v2') # 384-dim output

# Sample documents

documents = [
    "Meta developed LLaMA, a large language model.",
    "Qdrant is a vector database designed for similarity search.",
    "Retrieval-Augmented Generation combines search with generation.",    
]

vectors = model.encode(documents)


output = []
for i, (doc, vec) in enumerate(zip(documents, vectors)):
    output.append({
        "id": i + 1,
        "vector": vec.tolist(),
        "payload": {
            "text" : doc
        }  # Convert numpy array to list for JSON serialization
    })

# Wite to file 
with open('vectors.json', 'w') as f:
    json.dump(output, f, indent=2)

print("Embeddings saved to vectors.json")