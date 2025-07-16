import pandas as pd
from langchain.schema import Document
from langchain_community.document_loaders import PyPDFLoader
from langchain.text_splitter import RecursiveCharacterTextSplitter
from langchain_community.vectorstores import FAISS
from langchain_community.embeddings import OpenAIEmbeddings
from langchain.chains import RetrievalQA
from langchain_community.chat_models import ChatOpenAI
import tempfile

def load_csv(file_path):
    df = pd.read_csv(file_path)
    # Convert the whole CSV content to a string for embedding
    text = df.to_string()
    # Wrap it in a Document object (required by LangChain)
    return [Document(page_content=text)]

def process_and_answer(file, question):
    apiKey = "no-key"  # your OpenAI API key
    # Save uploaded file temporarily
    with tempfile.NamedTemporaryFile(delete=False, suffix=f'.{file.type.split("/")[-1]}') as tmp:
        tmp.write(file.read())
        temp_path = tmp.name    

    # Determine file type and load accordingly
    if file.type == "application/pdf":
        loader = PyPDFLoader(temp_path)
        docs = loader.load()
    elif file.type == "text/csv":
        docs = load_csv(temp_path)
    else:
        raise ValueError("Unsupported file type")

    # Split text into chunks
    splitter = RecursiveCharacterTextSplitter(chunk_size=500, chunk_overlap=50)
    chunks = splitter.split_documents(docs)

    # Embedding + indexing 
    embeddings = OpenAIEmbeddings(openai_api_key=apiKey)
    vectordb = FAISS.from_documents(chunks, embeddings)
    retriever = vectordb.as_retriever()

    # QA chain
    qa = RetrievalQA.from_chain_type(
        llm=ChatOpenAI(openai_api_key=apiKey, temperature=0),
        retriever=retriever,
        return_source_documents=True
    )

    result = qa({"query": question})
    return result['result'], result['source_documents']
