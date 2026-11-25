# Demo Documents

These five DOCX files provide grounded content for the evaluation dataset in
`database/seed/evaluation-dataset.jsonl`:

| File | Topic |
|------|-------|
| `refund-policy.docx` | Refund window, digital goods, request process |
| `hr-handbook.docx` | Health insurance, vacation, remote work, learning budget |
| `support-sla.docx` | Support hours, response and resolution targets |
| `expense-policy.docx` | Reimbursement, approval limits, travel |
| `security-policy.docx` | Passwords, training, phishing, data handling |

Regenerate them (standard library only, no downloads):

```bash
python3 scripts/generate-demo-docs.py
```

Upload them from the **Documents** page in the web UI, set access level to
`Team` or `Public`, wait for status `Ready`, then ask the evaluation questions
in the chat. Set the same documents' IDs in the `relevant_doc_ids` fields of
the dataset when running the evaluation pipeline.
