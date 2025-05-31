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

  - Now we have real vector embedding saved to a file.
     
4. Embedding - Using sentence-transformers to generate embeddings
    - The vector.json file now contains the data in a format that can be easily imported into Qdrant for efficient similarity seach operation. The next logical step would be to upload these vector.json entries into your running Qdrant instance.
    - In order to putt data in Qdrant we need to instal qdrant-client and then use rest api to upload data => pip install qdrant-client
    - Create pythin script like upload_vectors_to_qdrant.py
      
