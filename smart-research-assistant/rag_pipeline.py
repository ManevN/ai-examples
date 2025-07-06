from langchain_community.document_loaders import PyPDFLoader
from langchain.text_splitter import RecursiveCharacterTextSplitter
from langchain_community.vectorstores import FAISS
from langchain_community.embeddings import OpenAIEmbeddings
from langchain.chains import RetrievalQA
from langchain_community.chat_models import ChatOpenAI
import tempfile

def process_and_answer(file, question):
    # Save uploaded file temporarily
    with tempfile.NamedTemporaryFile(delete=False, suffix='.pdf') as tmp:
        tmp.write(file.read())
        temp_path = tmp.name    

    # Load & split
    loader = PyPDFLoader(temp_path)
    docs = loader.load()
    splitter = RecursiveCharacterTextSplitter(chunk_size=500, chunk_overlap=50)
    chunks = splitter.split_documents(docs)

    # Embedding + indexing 
    embeddings = OpenAIEmbeddings(openai_api_key="no-key")
    vectordb = FAISS.from_documents(chunks, embeddings)
    retriever = vectordb.as_retriever()

    # QA chain
    qa = RetrievalQA.from_chain_type(
        llm=ChatOpenAI(openai_api_key="no-key",temperature=0),
        retriever=retriever,
        return_source_documents=True
    )

    result = qa({"query": question})
    return result['result'], result['source_documents']
