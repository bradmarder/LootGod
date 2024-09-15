import axios, { AxiosError } from "axios";
import Swal from "sweetalert2";

const never = new Promise(() => { });

export const setAxiosInterceptors = () => {
	axios.interceptors.response.use(
		x => x,
		error => {
			if (axios.isCancel(error)) {
				return never;
			}

			console.error(error);

			if (error instanceof AxiosError && error.config?.method === 'post') {
				Swal.fire({
					icon: 'error',
					title: error.code,
					text: error.response?.data || error.response?.statusText || error.message,
				});
				return never;
			}

			return Promise.reject(error);
		}
	);
};