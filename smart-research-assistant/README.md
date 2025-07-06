Smart Research Assistant (LangChain + Streamlit)

This project is simple Retrieval-Augmented Generation (RAG) pipeline build with LangChain framework and Streamlit for the UI.
It allows user to upload a PDF document and ask questions about its content.

This is PDF question-answering tool build using LangChain and OpenAI models, with a user interface built in Streamlit. It converts the document into chunks using a recursive character splitter, embeds them using OpenAI's embedding model and retrieves relevant chunks via FAISS. The final answer is generated using a GPT model through a RetrievalQA pipeline.


Key Technologies used:
- LangChain: A framework for building language model applications. In this case, it is used to:
    - Load and split the document (PyPDFLoader + RecursiveCharacterTextSplitter)
    - Create embeddings using OpenAI Embeddings
    - Store and retrieve chunks with FIASS, an efficient vector database
    - Run a RetrieveQA chain using OpenAI's GPT model for answering questions

- OpenAI models:
    - text-embedding-ada-002 or newer models (viia OpenAIEmbeddings) - used to convert text chunks into vector embeddings
    - gpt-3.5 turbo / gpt-4 (via ChatOpenAI) - used as the reasoning model that processes the retriever chunks and answers questions.  

- Stremlit: A lightweight Python library framework used to create a simple web UI for:
    - Uploading the PDF file
    - Typing a question
    - Displaying the answer and source snippet 

- RecursiveCharacterTextSplitter: A strategy to split documents into overlapping chunks of text, which is helpful for maintaining context across chunk boundaries. In this case, chunk_size=500 and chunk_overlap=50


