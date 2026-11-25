#!/usr/bin/env python3
"""Generate demo DOCX documents for RAG evaluation and demonstration scenarios.

Uses only the Python standard library (DOCX is a ZIP of XML parts).
"""

from __future__ import annotations

import zipfile
from pathlib import Path

DOCS: dict[str, str] = {
    "refund-policy": """Employee and Customer Refund Policy

1. Standard Products
Standard products may be returned for a full refund within 30 days of delivery.
To request a refund, submit a request through the support portal and include your order number.
Refunds are processed to the original payment method within 10 business days of approval.

2. Digital Products
Digital products such as software licenses and downloadable content are non-refundable once downloaded or activated.
Exceptions are granted only when the product was defective at the time of delivery.

3. Exceptions
Shipping costs are non-refundable unless the return was caused by an error on our side.
Gift cards and promotional items cannot be refunded for cash.
""",
    "hr-handbook": """Employee Handbook

1. Health Insurance
All full-time employees are eligible for the company health insurance plan after 90 days of continuous employment.
Part-time employees may purchase coverage at their own cost after 180 days.

2. Vacation
Full-time employees receive 20 paid vacation days per calendar year.
Up to five unused vacation days may be carried over into the next calendar year.
Vacation requests require manager approval at least two weeks in advance.

3. Remote Work
Employees may work remotely up to three days per week with manager approval.
Full remote arrangements require written approval from the department head.

4. Learning and Development
Each employee has an annual learning budget of 1500 USD for courses, conferences, and certification exams.
""",
    "support-sla": """Support Service Level Agreement

1. Operating Hours
The support team operates from 09:00 to 18:00 CET, Monday through Friday.
Emergency coverage is available 24/7 for critical incidents.

2. Response Times
Critical incidents receive a first response within 15 minutes.
High-priority tickets receive a first response within 1 hour.
Standard tickets receive a first response within 8 business hours.

3. Resolution Targets
High-priority tickets are targeted for resolution within 4 business hours.
Standard tickets are targeted for resolution within 3 business days.
""",
    "expense-policy": """Expense and Travel Policy

1. Reimbursement
A valid receipt and a completed expense form are required for reimbursement.
Approved reimbursements are paid within 10 business days.
Expenses must be submitted within 30 days of the purchase date.

2. Approval Limits
Team leads can approve single expenses up to 500 USD.
Department heads can approve single expenses up to 2500 USD.
Amounts above 2500 USD require finance director approval.

3. Travel
Travel insurance is included for all approved business trips.
Employees must use company-managed devices when accessing production systems during travel.
""",
    "security-policy": """Information Security Policy

1. Passwords
Passwords must be at least 12 characters long and include an uppercase letter, a lowercase letter, a number, and a symbol.
Password managers are provided to all employees.

2. Training
Security awareness training is required annually for all employees.
New hires must complete training within 30 days of starting.

3. Phishing
If an employee suspects a phishing email, they must report it to the security team immediately using the reporting button in their mail client.

4. Data Handling
Sensitive customer data must always be masked in logs and non-production environments.
Personal devices are not permitted to access production systems; only company-managed devices are allowed.
""",
}


def escape_xml(text: str) -> str:
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def make_docx(path: Path, title: str, body: str) -> None:
    paragraphs = "".join(
        f'<w:p><w:r><w:t xml:space="preserve">{escape_xml(line)}</w:t></w:r></w:p>'
        for line in body.strip().splitlines()
    )
    document = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">'
        f"<w:body>{paragraphs}<w:sectPr/></w:body></w:document>"
    )
    content_types = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
        '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
        '<Default Extension="xml" ContentType="application/xml"/>'
        '<Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>'
        "</Types>"
    )
    rels = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
        '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>'
        "</Relationships>"
    )

    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as zf:
        zf.writestr("[Content_Types].xml", content_types)
        zf.writestr("_rels/.rels", rels)
        zf.writestr("word/document.xml", document)


def main() -> None:
    out = Path(__file__).resolve().parent.parent / "docs" / "demo-documents"
    out.mkdir(parents=True, exist_ok=True)
    for name, body in DOCS.items():
        make_docx(out / f"{name}.docx", name, body)
        print(f"Created {out / (name + '.docx')}")
    print(f"\nUpload these via the Documents page. Evaluation dataset: database/seed/evaluation-dataset.jsonl")


if __name__ == "__main__":
    main()