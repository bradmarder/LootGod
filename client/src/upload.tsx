import { useState } from 'react';
import { Alert, Form, ToastContainer, Toast } from 'react-bootstrap';
import axios from 'axios';

export default function Upload(props: { refreshCache: () => void }) {

	const [loading, setLoading] = useState(false);
	const [key, setKey] = useState(1);
	const [uploaded, setUploaded] = useState<string[]>([]);

	const uploadDump = (file: File) => {
		setLoading(true);
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
				setLoading(false);
			});
	};
	const passwordHref = '/api/GetPasswords?' + new URLSearchParams({
		playerKey: localStorage.getItem('key')!,
	}).toString();

	return (
		<Alert variant='primary'>
			<h4>Upload Guild/Raid Dumps (Admin only)</h4>
			<hr />
			<Alert variant='info'>NOTE! It is safe to upload duplicate raid dumps - the system automatically deduplicates</Alert>
			<hr />
			<Form>
				<input type='file' key={key} accept='.txt,.zip' disabled={loading} onChange={e => uploadDump(e.target.files![0]!)} />
				<hr />
				{uploaded.length > 0 &&
					<ToastContainer className="position-static">
						{uploaded.map(x =>
							<Toast key={x} bg={'success'} onClose={() => setUploaded(uploaded.filter(u => u !== x))}>
								<Toast.Header>
									<strong className='me-auto'>Upload Success</strong>
								</Toast.Header>
								<Toast.Body>{x}</Toast.Body>
							</Toast>
						)}
				  </ToastContainer>
				}
				<hr />
				<a target="_blank" rel="noreferrer" href={passwordHref}>Download Master Password Links (Leader Only)</a>
			</Form>
		</Alert>
	);
}