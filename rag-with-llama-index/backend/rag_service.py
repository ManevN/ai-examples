import os
import config
from llama_index.core import (
    VectorStoreIndex,
    SimpleDirectoryReader,
    StorageContext,
    Settings,
    load_index_from_storage,
    Document
)
from llama_index.llms.openai import OpenAI
from llama_index.embeddings.huggingface import HuggingFaceEmbedding

class RAGService:
    def __init__(self):
        print("Initializing RAG Service...")

        #Configure global settings for LlamaIndex
        Settings.llm = OpenAI(model = config.LLM_MODEL, api_key = config.OPENAI_API_KEY)
        Settings.embed_model = HuggingFaceEmbedding(model_name = config.EMBED_MODEL)
        Settings.chink_size = config.CHUNK_SIZE
        Settings.chunk_overlap = config.CHUNK_OVERLAP

        self._load_index()
        self.query_engine = self.index.as_query_engine()

    def _load_index(self):
        "Loads the index from storage or builds a new one if it doesn't exists."
        if os.path.exists(config.STORAGE_DIR) and os.listdir(config.STORAGE_DIR):
            print("Loading index from storage...")
            storage_context = StorageContext.from_defaults(persist_dir=config.STORAGE_DIR)
            self.index = load_index_from_storage(storage_context)
        else:
            print("No existing index found. Buiding a new one...")
            os.makedirs(config.STORAGE_DIR, exist_ok=True)
            documents = SimpleDirectoryReader(config.DATA_DIR).load_data()
            if not documents:
                print("No documents found in the data directory. The index will be empty.")
            self.index = VectorStoreIndex.from_documents(documents)
            self.index.storage_context.persist(persist_dir=config.STORAGE_DIR)

    def query(self, prompt: str) -> str:
        response = self.query_engine.query(prompt)
        return str(response)
    
    def refresh_document(self, file_path: str):
        doc = SimpleDirectoryReader(input_files=[file_path]).load_data()[0]
        doc.id_ = file_path # Use file path as the unique document ID

        self.index.insert(doc)


    def delete_document(self, file_path: str):
        self.index.delete_ref_doc(file_path, delete_from_docstore=True)
    
    def persist_index(self):
        self.index.storage_context.persist(persist_dir=config.STORAGE_DIR)