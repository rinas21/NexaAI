import { useEffect, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import {
  Bot,
  FileText,
  MessageSquarePlus,
  PanelLeft,
  Send,
  Sparkles,
  Trash2,
  UserRound,
} from 'lucide-react';
import {
  useConversation,
  useConversations,
  useCreateConversation,
  useDeleteConversation,
  useSendMessage,
} from '@hooks/useChat';
import { Button } from '@components/ui/Button';
import { EmptyState } from '@components/ui/EmptyState';
import { LoadingSpinner } from '@components/ui/LoadingSpinner';
import { Skeleton } from '@components/ui/Skeleton';
import { cn } from '@utils/cn';
import { formatDateTime, formatDuration, formatRelative } from '@utils/format';
import type { ChatMessage } from '@/types';

interface StreamState {
  id: string;
  full: string;
  shown: string;
  done: boolean;
}

function TypingIndicator() {
  return (
    <div className="flex items-center gap-1.5" aria-label="Assistant is typing" role="status">
      {[0, 1, 2].map((dot) => (
        <span
          key={dot}
          className="h-1.5 w-1.5 animate-bounce rounded-full bg-neutral-400 dark:bg-neutral-500"
          style={{ animationDelay: `${dot * 120}ms` }}
        />
      ))}
    </div>
  );
}

function CitationChips({ citations }: { citations: NonNullable<ChatMessage['citations']> }) {
  if (citations.length === 0) return null;
  return (
    <div className="mt-3 flex flex-wrap gap-2 border-t border-neutral-200/70 pt-3 dark:border-neutral-700/70">
      <span className="text-[11px] font-medium uppercase tracking-wide text-neutral-400">Sources</span>
      {citations.map((citation, index) => (
        <span
          key={`${citation.chunkId ?? citation.documentId ?? 'citation'}-${index}`}
          title={citation.snippet ?? citation.documentTitle}
          className="inline-flex max-w-[220px] items-center gap-1.5 rounded-full bg-white/70 px-2.5 py-1 text-[11px] font-medium text-primary-700 shadow-sm dark:bg-neutral-700/60 dark:text-primary-300"
        >
          <FileText className="h-3 w-3 shrink-0" />
          <span className="truncate">{citation.documentTitle}</span>
          {citation.similarityScore !== undefined && (
            <span className="opacity-60">{Math.round(citation.similarityScore * 100)}%</span>
          )}
        </span>
      ))}
    </div>
  );
}

function MessageRow({ message, stream }: { message: ChatMessage; stream?: StreamState }) {
  const isUser = message.role === 'user';
  const content = stream && stream.id === message.id ? stream.shown : message.content;

  return (
    <div className={cn('flex gap-3', isUser ? 'justify-end' : 'justify-start')}>
      {!isUser && (
        <span className="mt-1 flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-primary-500 to-indigo-600 text-white shadow-sm">
          {message.role === 'tool' ? <Bot className="h-4 w-4" /> : <Sparkles className="h-4 w-4" />}
        </span>
      )}
      <div className={cn('max-w-[85%] min-w-0', isUser && 'flex flex-col items-end')}>
        <div className={isUser ? 'message-user' : message.role === 'tool' ? 'message-tool' : 'message-assistant'}>
          {isUser ? (
            <p className="whitespace-pre-wrap">{content}</p>
          ) : (
            <div className={cn(stream && !stream.done && 'animate-fade-in')}>
              <ReactMarkdown
                remarkPlugins={[remarkGfm]}
                components={{
                  p: ({ children }) => <p className="mb-2 last:mb-0">{children}</p>,
                  ul: ({ children }) => <ul className="mb-2 list-disc space-y-1 pl-5">{children}</ul>,
                  ol: ({ children }) => <ol className="mb-2 list-decimal space-y-1 pl-5">{children}</ol>,
                  a: ({ href, children }) => (
                    <a href={href ?? '#'} target="_blank" rel="noreferrer" className="text-primary-700 underline dark:text-primary-300">
                      {children}
                    </a>
                  ),
                  code: ({ className: codeClassName, children }) =>
                    codeClassName ? (
                      <code className={cn('code-block block', codeClassName)}>{children}</code>
                    ) : (
                      <code className="inline-code">{children}</code>
                    ),
                  pre: ({ children }) => <pre className="code-block my-2 overflow-x-auto">{children}</pre>,
                }}
              >
                {content}
              </ReactMarkdown>
              {stream && !stream.done && (
                <span className="ml-0.5 inline-block h-4 w-1.5 animate-pulse bg-primary-500 align-middle" aria-hidden="true" />
              )}
            </div>
          )}
        </div>
        {!isUser && <CitationChips citations={message.citations ?? []} />}
        <p className="mt-1 px-1 text-[10px] text-neutral-400">
          {formatDateTime(message.createdAt)}
          {message.latencyMs ? ` · ${formatDuration(message.latencyMs)}` : ''}
        </p>
      </div>
      {isUser && (
        <span className="mt-1 flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-neutral-200 text-neutral-600 dark:bg-neutral-700 dark:text-neutral-200">
          <UserRound className="h-4 w-4" />
        </span>
      )}
    </div>
  );
}

export function ChatPage() {
  const [activeConversationId, setActiveConversationId] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const [agentMode, setAgentMode] = useState(false);
  const [stream, setStream] = useState<StreamState | null>(null);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const endRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const conversations = useConversations();
  const createConversation = useCreateConversation();
  const deleteConversation = useDeleteConversation();
  const sendMessage = useSendMessage();
  const conversation = useConversation(activeConversationId);

  useEffect(() => {
    if (conversation.data?.messages) setMessages(conversation.data.messages);
  }, [conversation.data]);

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' });
  }, [messages, stream?.shown, sendMessage.isPending]);

  useEffect(() => {
    if (!stream || stream.done) return;
    const tokens = stream.full.split(/(\s+)/);
    let index = 0;
    const timer = window.setInterval(() => {
      index = Math.min(index + 2, tokens.length);
      const shown = tokens.slice(0, index).join('');
      setStream((current) =>
        current ? { ...current, shown, done: index >= tokens.length } : null,
      );
      if (index >= tokens.length) window.clearInterval(timer);
    }, 22);
    return () => window.clearInterval(timer);
    // Only restart the stream when a new reply starts.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [stream?.id]);

  const startNewConversation = async () => {
    try {
      const created = await createConversation.mutateAsync({ title: 'New conversation' });
      setActiveConversationId(created.id);
      setMessages([]);
      setSidebarOpen(false);
      textareaRef.current?.focus();
    } catch {
      // createConversation surfaces the error via toast
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Delete this conversation?')) return;
    try {
      await deleteConversation.mutateAsync(id);
      if (activeConversationId === id) {
        setActiveConversationId(null);
        setMessages([]);
      }
    } catch {
      // deleteConversation surfaces the error via toast
    }
  };

  const handleSend = async () => {
    const text = input.trim();
    if (!text || sendMessage.isPending) return;

    const tempId = `temp-${Date.now()}`;
    const optimistic: ChatMessage = {
      id: tempId,
      role: 'user',
      content: text,
      createdAt: new Date().toISOString(),
    };
    setMessages((current) => [...current, optimistic]);
    setInput('');
    setStream(null);

    try {
      const response = await sendMessage.mutateAsync({
        message: text,
        conversationId: activeConversationId,
        useAgent: agentMode,
      });
      if (!activeConversationId) setActiveConversationId(response.conversationId);
      const replyId = response.messageId || `reply-${Date.now()}`;
      const assistantMessage: ChatMessage = {
        id: replyId,
        conversationId: response.conversationId,
        role: 'assistant',
        content: response.content,
        citations: response.citations ?? [],
        createdAt: new Date().toISOString(),
        latencyMs: response.totalLatencyMs,
      };
      setMessages((current) => [...current.filter((item) => item.id !== tempId), optimistic, assistantMessage]);
      if (response.content) setStream({ id: replyId, full: response.content, shown: '', done: false });
    } catch {
      setMessages((current) => current.filter((item) => item.id !== tempId));
      setInput(text);
    }
  };

  const handleKeyDown = (event: ReactKeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      void handleSend();
    }
  };

  const conversationItems = conversations.data ?? [];
  const activeConversation = conversationItems.find((item) => item.id === activeConversationId);

  return (
    <div className="flex h-[calc(100vh-9rem)] min-h-[560px] overflow-hidden rounded-2xl border border-neutral-200 bg-white shadow-card dark:border-neutral-700 dark:bg-neutral-900">
      {sidebarOpen && (
        <div
          className="absolute inset-0 z-20 bg-neutral-950/40 md:hidden"
          onClick={() => setSidebarOpen(false)}
          aria-hidden="true"
        />
      )}
      <aside
        className={cn(
          'w-72 shrink-0 flex-col border-r border-neutral-200 bg-neutral-50/70 dark:border-neutral-800 dark:bg-neutral-900/60 md:flex',
          sidebarOpen ? 'absolute inset-y-0 left-0 z-30 flex w-[280px] shadow-soft' : 'hidden',
        )}
        aria-label="Conversations"
      >
        <div className="border-b border-neutral-200 p-4 dark:border-neutral-800">
          <Button
            fullWidth
            leftIcon={<MessageSquarePlus className="h-4 w-4" />}
            onClick={() => void startNewConversation()}
            isLoading={createConversation.isPending}
          >
            New conversation
          </Button>
        </div>
        <div className="flex-1 space-y-1 overflow-y-auto p-3">
          {conversations.isLoading ? (
            <>
              <Skeleton className="h-12 w-full" />
              <Skeleton className="h-12 w-full" />
              <Skeleton className="h-12 w-full" />
            </>
          ) : conversationItems.length === 0 ? (
            <p className="px-3 py-6 text-center text-xs text-neutral-500 dark:text-neutral-400">
              No conversations yet. Start one above.
            </p>
          ) : (
            conversationItems.map((item) => (
              <div
                key={item.id}
                className={cn(
                  'group flex items-center gap-2 rounded-xl px-3 py-2.5 transition-colors',
                  item.id === activeConversationId
                    ? 'bg-primary-50 text-primary-800 dark:bg-primary-900/25 dark:text-primary-200'
                    : 'text-neutral-600 hover:bg-neutral-100 dark:text-neutral-400 dark:hover:bg-neutral-800',
                )}
              >
                <button
                  type="button"
                  onClick={() => {
                    setActiveConversationId(item.id);
                    setSidebarOpen(false);
                  }}
                  className="min-w-0 flex-1 text-left"
                >
                  <span className="block truncate text-sm font-medium">{item.title || 'Untitled chat'}</span>
                  <span className="block text-[11px] text-neutral-400">{formatRelative(item.updatedAt)}</span>
                </button>
                <button
                  type="button"
                  onClick={() => void handleDelete(item.id)}
                  aria-label={`Delete ${item.title || 'conversation'}`}
                  className="btn-icon btn-ghost h-7 w-7 text-neutral-400 opacity-0 hover:text-red-600 group-hover:opacity-100 focus:opacity-100"
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              </div>
            ))
          )}
        </div>
        <div className="border-t border-neutral-200 p-4 text-xs text-neutral-500 dark:border-neutral-800 dark:text-neutral-400">
          <span className="flex items-center gap-2">
            <Sparkles className="h-3.5 w-3.5 text-primary-500" />
            Grounded in your documents
          </span>
        </div>
      </aside>

      <section className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 shrink-0 items-center gap-3 border-b border-neutral-200 px-4 dark:border-neutral-800">
          <button
            type="button"
            onClick={() => setSidebarOpen(true)}
            aria-label="Open conversation list"
            className="btn-icon btn-ghost text-neutral-500 md:hidden"
          >
            <PanelLeft className="h-5 w-5" />
          </button>
          <div className="min-w-0 flex-1">
            <h2 className="truncate text-sm font-semibold text-neutral-800 dark:text-neutral-100">
              {activeConversation?.title || 'New conversation'}
            </h2>
            <p className="text-[11px] text-neutral-400">
              {agentMode ? 'Agent mode · tools enabled' : 'Answers cited from your knowledge base'}
            </p>
          </div>
          <button
            type="button"
            onClick={() => setAgentMode((enabled) => !enabled)}
            aria-pressed={agentMode}
            className={cn(
              'inline-flex items-center gap-1.5 rounded-lg border px-2.5 py-1.5 text-xs font-medium transition-colors',
              agentMode
                ? 'border-purple-300 bg-purple-50 text-purple-700 dark:border-purple-700 dark:bg-purple-900/30 dark:text-purple-300'
                : 'border-neutral-200 text-neutral-500 hover:bg-neutral-50 dark:border-neutral-700 dark:hover:bg-neutral-800',
            )}
          >
            <Bot className="h-3.5 w-3.5" /> Agent mode
          </button>
        </header>

        <div className="flex-1 overflow-y-auto px-4 py-6 sm:px-6">
          {messages.length === 0 ? (
            <div className="flex h-full items-center justify-center">
              <EmptyState
                icon={<Sparkles className="h-6 w-6" />}
                title="Ask anything about your knowledge base"
                description="Start a conversation, or switch on agent mode to let tools gather context for you."
                action={
                  <div className="flex flex-wrap justify-center gap-2">
                    {['Summarize our latest policy', 'What changed this quarter?', 'Compare two documents'].map((prompt) => (
                      <button
                        key={prompt}
                        type="button"
                        onClick={() => setInput(prompt)}
                        className="rounded-full border border-neutral-200 bg-white px-3 py-1.5 text-xs text-neutral-600 transition-colors hover:border-primary-300 hover:text-primary-700 dark:border-neutral-700 dark:bg-neutral-800 dark:text-neutral-300"
                      >
                        {prompt}
                      </button>
                    ))}
                  </div>
                }
              />
            </div>
          ) : (
            <div className="mx-auto max-w-3xl space-y-6">
              {messages.map((message) => (
                <MessageRow key={message.id} message={message} stream={stream ?? undefined} />
              ))}
              {sendMessage.isPending && (
                <div className="flex items-center gap-3">
                  <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-gradient-to-br from-primary-500 to-indigo-600 text-white">
                    <Sparkles className="h-4 w-4" />
                  </span>
                  <div className="message-assistant">
                    <TypingIndicator />
                  </div>
                </div>
              )}
              <div ref={endRef} />
            </div>
          )}
        </div>

        <div className="shrink-0 border-t border-neutral-200 bg-white p-4 dark:border-neutral-800 dark:bg-neutral-900">
          <div className="mx-auto max-w-3xl">
            <div className="relative rounded-2xl border border-neutral-200 bg-neutral-50 shadow-sm transition-colors focus-within:border-primary-400 dark:border-neutral-700 dark:bg-neutral-800/60">
              <textarea
                ref={textareaRef}
                value={input}
                onChange={(event) => setInput(event.target.value)}
                onKeyDown={handleKeyDown}
                rows={2}
                aria-label="Message"
                placeholder={agentMode ? 'Ask an agent to investigate…' : 'Ask a question about your documents…'}
                className="max-h-40 w-full resize-none bg-transparent px-4 py-3 text-sm text-neutral-800 outline-none placeholder:text-neutral-400 dark:text-neutral-100"
              />
              <div className="flex items-center justify-between gap-2 px-3 pb-3">
                <p className="text-[11px] text-neutral-400">Enter to send · Shift+Enter for a new line</p>
                <Button
                  size="sm"
                  onClick={() => void handleSend()}
                  disabled={!input.trim()}
                  isLoading={sendMessage.isPending}
                  rightIcon={<Send className="h-3.5 w-3.5" />}
                >
                  Send
                </Button>
              </div>
            </div>
          </div>
        </div>
      </section>

      {sendMessage.isPending && (
        <span className="sr-only" role="status">
          <LoadingSpinner size="sm" /> Sending message
        </span>
      )}
    </div>
  );
}
