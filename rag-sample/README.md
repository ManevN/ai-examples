Project steps

1. Qdrant Setup: Local server up and running via Docker.
   - Run Qdrant locally with docker docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant
   - Pull the qdrant/qdrant image (if not already download)
   - Start the Qdrant server and maps its ports to your machine
      - 6333 - REST API
      - 6334 - gRPC
   - Access dashboard on http://localhost:6333/dashboard
   - Test if it works http://localhost:6333/collections
2. Generate Embeddings with Python
   - Install python
   - Install required Python package => pip install sentence-transformers
   - Create a small pythin script like generate_vectors.py
   - Using sentence-transformers to convert text into embeddings (vectors).
   - Run the script => python generate_vectors.py
   - It will generate a file called vector.json like
  <pre><code>
    [  
      {
        "id": 1,
        "vector": [0.12, -0.04, ..., 0.09],
        "payload": { "text": "Meta developed LLaMA..." }
      }
    ]
  </code></pre>

  - Now we have real vector embedding saved to a file, saved vectors to a file (vector.json).
     
4.  Upload to Qdrant – Load precomputed embeddings and insert them into your Qdrant collection for semantic search.
    - The vector.json file now contains the data in a format that can be easily imported into Qdrant for efficient similarity seach operation. The next logical step would be to upload these vector.json entries into your running Qdrant instance.
    - In order to putt data in Qdrant we need to instal qdrant-client and then use rest api to upload data => pip install qdrant-client
    - Create pythin script like upload_vectors_to_qdrant.py
        - Loading pre-generated embeddings from vectors.json (which were already created by generate_vectors.py).
        - Uploading those vectors to Qdrant.
        - Optionally performing a search using new embeddings (you encode the query using SentenceTransformer).
      

This process essentially moves your text data from being raw strings, to semantically rich numerical vectors, and then stores them in a specialized database (Qdrant) designed to quickly find similar vectors. You've now set up a basic end-to-end vector search pipeline and successfully completed the "data ingestion" phase of a Retrieval-Augmented Generation (RAG) pipeline.

Now here is next phase => Phase 2 - Retrieval & Generation (Basic RAG)


