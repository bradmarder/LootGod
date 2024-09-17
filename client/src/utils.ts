import axios, { AxiosError } from "axios";
import Swal from "sweetalert2";

export const setAxiosInterceptors = () => {
	axios.interceptors.response.use(
		x => x,
		async error => {

			// catch abort errors and throw null rejection to avoid unhandledrejection error page
			if (axios.isCancel(error)) {
				return Promise.reject();
			}

			console.error(error);

			if (error instanceof AxiosError && error.config?.method === 'post') {
				await Swal.fire({
					icon: 'error',
					title: error.code,
					text: error.response?.data || error.response?.statusText || error.message,
				});
			}

			return Promise.reject(error);
		}
	);
};