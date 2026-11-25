import { useRef, useState, type DragEvent } from 'react';
import toast from 'react-hot-toast';
import {
  AlertCircle,
  CheckCircle2,
  CloudUpload,
  FileSearch,
  FileText,
  Filter,
  Search,
  Shield,
  Trash2,
  X,
} from 'lucide-react';
import {
  useDeleteDocument,
  useDocument,
  useDocuments,
  useUpdateDocumentAccess,
  useUploadDocument,
} from '@hooks/useDocuments';
import { Badge, type BadgeVariant } from '@components/ui/Badge';
import { Button } from '@components/ui/Button';
import { Card, CardContent, CardHeader, CardTitle } from '@components/ui/Card';
import { EmptyState } from '@components/ui/EmptyState';
import { Input } from '@components/ui/Input';
import { Modal } from '@components/ui/Modal';
import { Table, type Column } from '@components/ui/Table';
import { cn } from '@utils/cn';
import { documentFileName, formatBytes, formatDate, formatRelative } from '@utils/format';
import type { AccessLevel, Document as DocumentRecord, DocumentStatus } from '@/types';

const MAX_FILE_SIZE = 50 * 1024 * 1024;
const ALLOWED_EXTENSIONS = ['pdf', 'docx'];

function statusVariant(status: DocumentStatus): BadgeVariant {
  const value = status.toLowerCase();
  if (value.includes('index') || value.includes('ready') || value.includes('complete')) return 'success';
  if (value.includes('process') || value.includes('pending') || value.includes('upload')) return 'warning';
  if (value.includes('fail') || value.includes('error')) return 'danger';
  return 'neutral';
}

export function DocumentsPage() {
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<DocumentRecord | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [uploadPercent, setUploadPercent] = useState(0);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const documents = useDocuments();
  const detail = useDocument(selectedId);
  const uploadDocument = useUploadDocument();
  const deleteDocument = useDeleteDocument();
  const updateAccess = useUpdateDocumentAccess();

  const allDocuments = documents.data ?? [];
  const filtered = allDocuments.filter((document) => {
    const matchesSearch =
      document.title.toLowerCase().includes(search.toLowerCase()) ||
      documentFileName(document).toLowerCase().includes(search.toLowerCase());
    const matchesStatus = statusFilter === 'all' || document.status.toLowerCase() === statusFilter;
    return matchesSearch && matchesStatus;
  });

  const uploadFiles = async (files: File[]) => {
    for (const file of files) {
      const extension = file.name.split('.').pop()?.toLowerCase() ?? '';
      if (!ALLOWED_EXTENSIONS.includes(extension)) {
        toast.error(`${file.name} is not supported. Upload PDF or DOCX files.`);
        continue;
      }
      if (file.size > MAX_FILE_SIZE) {
        toast.error(`${file.name} exceeds the 50 MB limit.`);
        continue;
      }
      setUploadPercent(0);
      try {
        await uploadDocument.mutateAsync(file);
      } catch {
        // Mutation hook shows an error toast.
      } finally {
        setUploadPercent(0);
      }
    }
  };

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setIsDragging(false);
    void uploadFiles(Array.from(event.dataTransfer.files));
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await deleteDocument.mutateAsync(deleteTarget.id);
      if (selectedId === deleteTarget.id) setSelectedId(null);
      setDeleteTarget(null);
    } catch {
      // Mutation hook shows an error toast.
    }
  };

  const columns: Column<DocumentRecord>[] = [
    {
      key: 'name',
      header: 'Document',
      render: (document) => (
        <button
          type="button"
          onClick={() => setSelectedId(document.id)}
          className="flex min-w-0 items-center gap-3 text-left"
        >
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary-50 text-primary-600 dark:bg-primary-900/30 dark:text-primary-300">
            <FileText className="h-4 w-4" />
          </span>
          <span className="min-w-0">
            <span className="block truncate font-medium text-neutral-800 hover:text-primary-700 dark:text-neutral-100 dark:hover:text-primary-300">
              {document.title}
            </span>
            <span className="block truncate text-xs text-neutral-400">{documentFileName(document)}</span>
          </span>
        </button>
      ),
    },
    {
      key: 'size',
      header: 'Size',
      render: (document) => (
        <span className="text-neutral-500 dark:text-neutral-400">{formatBytes(document.fileSize)}</span>
      ),
    },
    {
      key: 'status',
      header: 'Status',
      render: (document) => (
        <Badge variant={statusVariant(document.status)} dot className="capitalize">
          {document.status}
        </Badge>
      ),
    },
    {
      key: 'chunks',
      header: 'Chunks',
      render: (document) => (
        <span className="text-neutral-500 dark:text-neutral-400">{document.chunkCount ?? 0}</span>
      ),
    },
    {
      key: 'access',
      header: 'Access',
      render: (document) => (
        <span className="capitalize text-neutral-500 dark:text-neutral-400">
          {document.accessLevel ?? 'Private'}
        </span>
      ),
    },
    {
      key: 'created',
      header: 'Added',
      render: (document) => (
        <span className="whitespace-nowrap text-neutral-500 dark:text-neutral-400">
          {formatRelative(document.createdAt)}
        </span>
      ),
    },
    {
      key: 'actions',
      header: '',
      render: (document) => (
        <div className="flex justify-end">
          <button
            type="button"
            onClick={() => setDeleteTarget(document)}
            aria-label={`Delete ${document.title}`}
            className="btn-icon btn-ghost text-neutral-400 hover:text-red-600"
          >
            <Trash2 className="h-4 w-4" />
          </button>
        </div>
      ),
    },
  ];

  const selectedDocument = detail.data;

  return (
    <div className="space-y-6 animate-fade-in">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h2 className="text-2xl font-semibold tracking-tight text-neutral-900 dark:text-white">Documents</h2>
          <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">
            Manage the files that power grounded answers.
          </p>
        </div>
        <Button onClick={() => fileInputRef.current?.click()} leftIcon={<CloudUpload className="h-4 w-4" />}>
          Upload documents
        </Button>
      </div>

      <div
        onDragOver={(event) => {
          event.preventDefault();
          setIsDragging(true);
        }}
        onDragLeave={() => setIsDragging(false)}
        onDrop={handleDrop}
        className={cn(
          'relative flex flex-col items-center justify-center rounded-2xl border-2 border-dashed px-6 py-10 text-center transition-all',
          isDragging
            ? 'border-primary-500 bg-primary-50/70 dark:bg-primary-900/20'
            : 'border-neutral-300 bg-white/60 hover:border-primary-300 dark:border-neutral-700 dark:bg-neutral-900/40',
        )}
      >
        <input
          ref={fileInputRef}
          type="file"
          multiple
          accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
          className="sr-only"
          aria-label="Upload PDF or DOCX documents"
          onChange={(event) => {
            void uploadFiles(Array.from(event.target.files ?? []));
            event.target.value = '';
          }}
        />
        <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-primary-50 text-primary-600 dark:bg-primary-900/30 dark:text-primary-300">
          <CloudUpload className="h-6 w-6" />
        </span>
        <p className="mt-4 font-medium text-neutral-800 dark:text-neutral-100">
          Drop PDF or DOCX files here
        </p>
        <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">
          or click “Upload documents” · maximum 50 MB per file
        </p>
        {uploadDocument.isPending && (
          <div className="mt-5 w-full max-w-sm">
            <div className="mb-1.5 flex justify-between text-xs text-neutral-500 dark:text-neutral-400">
              <span>Uploading…</span>
              <span>{uploadPercent}%</span>
            </div>
            <div className="h-1.5 overflow-hidden rounded-full bg-neutral-200 dark:bg-neutral-700">
              <div className="h-full rounded-full bg-primary-600 transition-all" style={{ width: `${uploadPercent}%` }} />
            </div>
          </div>
        )}
      </div>

      <Card>
        <CardHeader className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <CardTitle>Knowledge library</CardTitle>
            <p className="mt-1 text-sm text-neutral-500 dark:text-neutral-400">
              {filtered.length} of {allDocuments.length} documents
            </p>
          </div>
          <div className="flex w-full flex-col gap-3 sm:w-auto sm:flex-row">
            <Input
              type="search"
              placeholder="Search documents…"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              leftIcon={<Search className="h-4 w-4" />}
              className="sm:w-64"
              aria-label="Search documents"
            />
            <div className="relative">
              <Filter className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-neutral-400" />
              <select
                aria-label="Filter by status"
                value={statusFilter}
                onChange={(event) => setStatusFilter(event.target.value)}
                className="input cursor-pointer pl-9 sm:w-40"
              >
                <option value="all">All statuses</option>
                <option value="indexed">Indexed</option>
                <option value="ready">Ready</option>
                <option value="processing">Processing</option>
                <option value="pending">Pending</option>
                <option value="failed">Failed</option>
              </select>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <Table
            columns={columns}
            data={filtered}
            keyExtractor={(document) => document.id}
            isLoading={documents.isLoading}
            emptyTitle={allDocuments.length === 0 ? 'No documents yet' : 'No matching documents'}
            emptyDescription={
              allDocuments.length === 0
                ? 'Upload a PDF or DOCX to build your knowledge base.'
                : 'Try a different search or status filter.'
            }
            emptyIcon={<FileSearch className="h-6 w-6" />}
            onRowClick={(document) => setSelectedId(document.id)}
          />
        </CardContent>
      </Card>

      {selectedId && (
        <div className="fixed inset-0 z-40 flex justify-end">
          <div
            className="absolute inset-0 bg-neutral-950/40 backdrop-blur-sm"
            onClick={() => setSelectedId(null)}
            aria-hidden="true"
          />
          <aside
            role="dialog"
            aria-modal="true"
            aria-label="Document details"
            className="relative z-10 flex h-full w-full max-w-md flex-col border-l border-neutral-200 bg-white shadow-soft animate-slide-up dark:border-neutral-700 dark:bg-neutral-900"
          >
            <div className="flex items-start justify-between gap-4 border-b border-neutral-200 p-5 dark:border-neutral-800">
              <div className="min-w-0">
                <h3 className="truncate text-lg font-semibold text-neutral-900 dark:text-white">
                  {selectedDocument?.title ?? 'Document details'}
                </h3>
                <p className="mt-1 text-xs text-neutral-500 dark:text-neutral-400">
                  {selectedDocument ? `${formatBytes(selectedDocument.fileSize)} · Added ${formatDate(selectedDocument.createdAt)}` : 'Loading…'}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setSelectedId(null)}
                aria-label="Close document details"
                className="btn-icon btn-ghost text-neutral-500"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <div className="flex-1 space-y-6 overflow-y-auto p-5">
              {detail.isLoading ? (
                <div className="space-y-3">
                  <div className="skeleton h-20 w-full" />
                  <div className="skeleton h-32 w-full" />
                </div>
              ) : selectedDocument ? (
                <>
                  <div className="grid grid-cols-2 gap-3">
                    <div className="rounded-xl bg-neutral-50 p-3 dark:bg-neutral-800/60">
                      <p className="text-xs text-neutral-500 dark:text-neutral-400">Status</p>
                      <Badge variant={statusVariant(selectedDocument.status)} className="mt-2 capitalize">
                        {selectedDocument.status}
                      </Badge>
                    </div>
                    <div className="rounded-xl bg-neutral-50 p-3 dark:bg-neutral-800/60">
                      <p className="text-xs text-neutral-500 dark:text-neutral-400">Chunks</p>
                      <p className="mt-2 text-lg font-semibold">{selectedDocument.chunkCount ?? 0}</p>
                    </div>
                  </div>

                  {selectedDocument.errorMessage && (
                    <div className="flex gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-900/20 dark:text-red-300">
                      <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                      {selectedDocument.errorMessage}
                    </div>
                  )}

                  <div>
                    <label htmlFor="access-level" className="label flex items-center gap-2">
                      <Shield className="h-4 w-4 text-primary-600" /> Access level
                    </label>
                    <select
                      id="access-level"
                      className="input cursor-pointer capitalize"
                      value={selectedDocument.accessLevel ?? 'Private'}
                      disabled={updateAccess.isPending}
                      onChange={(event) => {
                        void updateAccess.mutateAsync({
                          id: selectedDocument.id,
                          accessLevel: event.target.value as AccessLevel,
                        });
                      }}
                    >
                      <option value="Private">Private</option>
                      <option value="Team">Team</option>
                      <option value="Public">Public</option>
                    </select>
                    <p className="mt-1.5 text-xs text-neutral-500 dark:text-neutral-400">
                      Who can use this document in answers.
                    </p>
                  </div>

                  <div>
                    <h4 className="text-sm font-semibold text-neutral-800 dark:text-neutral-100">
                      Indexed chunks
                    </h4>
                    <div className="mt-3 space-y-3">
                      {(selectedDocument.chunks ?? []).length === 0 ? (
                        <p className="rounded-xl bg-neutral-50 p-4 text-sm text-neutral-500 dark:bg-neutral-800/60 dark:text-neutral-400">
                          No chunks are available for this document yet.
                        </p>
                      ) : (
                        selectedDocument.chunks?.map((chunk) => (
                          <div key={chunk.id} className="rounded-xl border border-neutral-200 p-3 dark:border-neutral-700">
                            <div className="mb-2 flex items-center justify-between text-xs text-neutral-500 dark:text-neutral-400">
                              <span>Chunk {chunk.chunkIndex + 1}</span>
                              {chunk.tokenCount !== undefined && <span>{chunk.tokenCount} tokens</span>}
                            </div>
                            <p className="line-clamp-4 text-sm leading-relaxed text-neutral-700 dark:text-neutral-300">
                              {chunk.content}
                            </p>
                          </div>
                        ))
                      )}
                    </div>
                  </div>
                </>
              ) : (
                <EmptyState title="Document not found" description="It may have been deleted." />
              )}
            </div>

            <div className="flex items-center justify-between gap-3 border-t border-neutral-200 p-5 dark:border-neutral-800">
              <span className="flex items-center gap-1.5 text-xs text-green-600 dark:text-green-400">
                <CheckCircle2 className="h-3.5 w-3.5" /> Stored securely
              </span>
              <Button
                variant="destructive"
                size="sm"
                leftIcon={<Trash2 className="h-4 w-4" />}
                onClick={() => selectedDocument && setDeleteTarget(selectedDocument)}
                disabled={!selectedDocument}
              >
                Delete
              </Button>
            </div>
          </aside>
        </div>
      )}

      <Modal
        isOpen={Boolean(deleteTarget)}
        onClose={() => setDeleteTarget(null)}
        title="Delete document?"
        description="This removes the document and its indexed chunks from the knowledge base."
        size="sm"
        footer={
          <>
            <Button variant="ghost" onClick={() => setDeleteTarget(null)}>Cancel</Button>
            <Button variant="destructive" onClick={() => void handleDelete()} isLoading={deleteDocument.isPending}>
              Delete document
            </Button>
          </>
        }
      >
        <p className="text-sm text-neutral-600 dark:text-neutral-300">
          <span className="font-semibold">{deleteTarget?.title}</span> will no longer be available to the assistant.
        </p>
      </Modal>
    </div>
  );
}
