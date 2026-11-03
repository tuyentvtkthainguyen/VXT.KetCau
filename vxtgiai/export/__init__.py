from . import docx_out, pdf_out, pptx_out, dxf_out

FORMATS = {
    "docx": (docx_out.export, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "Word"),
    "pdf": (pdf_out.export, "application/pdf", "PDF"),
    "pptx": (pptx_out.export, "application/vnd.openxmlformats-officedocument.presentationml.presentation", "PowerPoint"),
    "dxf": (dxf_out.export, "image/vnd.dxf", "CAD"),
}
