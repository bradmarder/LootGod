import { useState } from 'react';
import { Alert, Form, ToastContainer, Toast } from 'react-bootstrap';
import axios from 'axios';

export default function Upload(props: { refreshCache: () => void }) {

	const [isLoading, setIsLoading] = useState(false);
	const [key, setKey] = useState(1);
	const [uploaded, setUploaded] = useState<string[]>([]);

	const uploadDump = (file: File) => {
		setIsLoading(true);
		const formData = new FormData();
		const offset = new Date().getTimezoneOffset();
		formData.append("file", file);
		axios
			.post('/ImportDump?offset=' + offset, formData)
			.then(() => {
				setUploaded([...uploaded, key + ' - ' + file.name]);
				props.refreshCache();
			})
			.finally(() => {
				setKey(x => x + 1);
				setIsLoading(false);
			});
	};

	return (
		<Alert variant='primary'>
			<h4>Upload Guild/Raid Dumps (Admin only)</h4>
			<hr />
			<Form>
				<input type='file' key={key} accept='.txt,.zip' disabled={isLoading} onChange={e => uploadDump(e.target.files![0]!)} />
				<hr />
				{uploaded.length > 0 &&
					<ToastContainer className="position-static">
						{uploaded.map(x =>
							<Toast key={x} bg={'success'} onClose={() => setUploaded(uploaded.filter(u => u !== x))}>
								<Toast.Header>
									<img src="/logo192.png" className="rounded me-2" alt="" width={20} height={20} />
									<strong className='me-auto'>Upload Success</strong>
									<small className="text-muted">just now</small>
								</Toast.Header>
								<Toast.Body>{x}</Toast.Body>
							</Toast>
						)}
				  </ToastContainer>
				}
				<hr />
				<a target="_blank" rel="noreferrer" href={'/api/GetPasswords?playerKey=' + localStorage.getItem('key')}>Download Master Password Links (Leader Only)</a>
			</Form>
		</Alert>
	);
}