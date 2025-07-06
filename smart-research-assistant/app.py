import streamlit as st
from rag_pipeline import process_and_answer

st.set_page_config(page_title="Smart Research Assistant", page_icon=":robot_face:", layout="wide")
st.title("Smart Research Assistant")
st.markdown("Upload a document and ask question about it.")

uploaded_file = st.file_uploader("Choose a file", type=["pdf"])

if uploaded_file:
    question = st.text_input("Ask a question about the document:")
    if question:
        with st.spinner("Thinking..."):
            answer, sources = process_and_answer(uploaded_file, question)
            st.success(answer)

            with st.expander("Sources"):
                for doc in sources:
                    st.markdown(f"• {doc.page_content[:300]}...")


st.write("Hello, Streamlit!")