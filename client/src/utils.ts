import { CanceledError } from "axios";

export const swallowAbortError = (error: unknown) => {
	if (error instanceof CanceledError && error.config?.signal?.aborted) { return; }
	throw error;
};
